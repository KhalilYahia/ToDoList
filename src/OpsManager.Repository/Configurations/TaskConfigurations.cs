using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;

namespace OpsManager.Repository.Configurations;

internal sealed class TaskTemplateConfiguration : EntityConfigurationBase<TaskTemplate>
{
    public override void Configure(EntityTypeBuilder<TaskTemplate> builder)
    {
        base.Configure(builder);
        builder.ToTable("task_templates", table =>
            table.HasCheckConstraint("ck_task_templates_duration", "\"default_duration_minutes\" IS NULL OR \"default_duration_minutes\" > 0"));
        builder.Property(entity => entity.Title).HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(8000);
        builder.Property(entity => entity.DefaultPriority).AsString();
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.DefaultDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.IsActive });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.DefaultDepartmentId });
    }
}

internal sealed class TaskTemplateItemConfiguration : EntityConfigurationBase<TaskTemplateItem>
{
    public override void Configure(EntityTypeBuilder<TaskTemplateItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("task_template_items");
        builder.Property(entity => entity.Title).HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(4000);
        builder.Property(entity => entity.EvidenceMode).AsString();
        builder.Property(entity => entity.MaxAttachments).HasDefaultValue(5);
        builder.Property(entity => entity.IsActive).HasDefaultValue(true);
        builder.HasOne<TaskTemplate>().WithMany().HasForeignKey(entity => entity.TaskTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.TaskTemplateId, entity.SortOrder })
            .IsUnique()
            .HasFilter("\"is_active\"");
    }
}

internal sealed class TaskTemplateItemAttachmentConfiguration : EntityConfigurationBase<TaskTemplateItemAttachment>
{
    public override void Configure(EntityTypeBuilder<TaskTemplateItemAttachment> builder)
    {
        base.Configure(builder);
        builder.ToTable("task_template_item_attachments");
        builder.Property(entity => entity.FileUrl).HasMaxLength(2048).IsRequired();
        builder.Property(entity => entity.FileType).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Caption).HasMaxLength(500);
        builder.HasOne<TaskTemplateItem>().WithMany().HasForeignKey(entity => entity.TaskTemplateItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.TaskTemplateItemId);
    }
}

internal sealed class TaskDistributionConfiguration : EntityConfigurationBase<TaskDistribution>
{
    public override void Configure(EntityTypeBuilder<TaskDistribution> builder)
    {
        base.Configure(builder);
        builder.ToTable("task_distributions", table =>
            table.HasCheckConstraint("ck_task_distributions_due_after_start", "\"due_at\" > \"scheduled_start_at\""));
        builder.Property(entity => entity.AssignmentMode).AsString();
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskTemplate>().WithMany().HasForeignKey(entity => entity.TaskTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskSchedule>().WithMany().HasForeignKey(entity => entity.TaskScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.DepartmentId, entity.OccurrenceDate });
        builder.HasIndex(entity => new { entity.TaskScheduleId, entity.OccurrenceDate, entity.ScheduledStartAt })
            .IsUnique()
            .HasFilter("\"task_schedule_id\" IS NOT NULL");
    }
}

internal sealed class TaskScheduleAssigneeConfiguration : EntityConfigurationBase<TaskScheduleAssignee>
{
    public override void Configure(EntityTypeBuilder<TaskScheduleAssignee> builder)
    {
        base.Configure(builder);
        builder.ToTable("task_schedule_assignees");
        builder.HasOne<TaskSchedule>().WithMany().HasForeignKey(entity => entity.TaskScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.TaskScheduleId, entity.UserId }).IsUnique();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.UserId });
    }
}

internal sealed class TaskScheduleConfiguration : EntityConfigurationBase<TaskSchedule>
{
    public override void Configure(EntityTypeBuilder<TaskSchedule> builder)
    {
        base.Configure(builder);
        builder.ToTable("task_schedules", table =>
        {
            table.HasCheckConstraint("ck_task_schedules_dates", "\"recurrence_end_date\" IS NULL OR \"recurrence_end_date\" >= \"recurrence_start_date\"");
            table.HasCheckConstraint(
                "ck_task_schedules_due_offset",
                "\"execution_due_day_offset\" IN (0, 1) AND (\"execution_due_day_offset\" = 1 OR \"execution_due_time\" > \"execution_start_time\")");
            table.HasCheckConstraint(
                "ck_task_schedules_recurrence_fields",
                "(\"recurrence_type\" = 'Daily' AND cardinality(\"weekdays\") = 0 AND cardinality(\"month_days\") = 0 AND NOT \"include_last_day_of_month\") OR " +
                "(\"recurrence_type\" = 'Weekly' AND cardinality(\"weekdays\") > 0 AND cardinality(\"month_days\") = 0 AND NOT \"include_last_day_of_month\") OR " +
                "(\"recurrence_type\" = 'Monthly' AND cardinality(\"weekdays\") = 0 AND (cardinality(\"month_days\") > 0 OR \"include_last_day_of_month\")) OR " +
                "(\"recurrence_type\" = 'SpecificDates' AND cardinality(\"weekdays\") = 0 AND cardinality(\"month_days\") = 0 AND NOT \"include_last_day_of_month\")");
        });
        builder.Property(entity => entity.AssignmentMode).AsString();
        builder.Property(entity => entity.RecurrenceType).AsString();
        ValueConverter<IReadOnlyList<Weekday>, short[]> weekdayConverter = new(
            values => values.Select(value => (short)value).ToArray(),
            values => values.Select(value => (Weekday)value).ToArray());
        ValueComparer<IReadOnlyList<Weekday>> weekdayComparer = new(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            values => values.Aggregate(0, (hash, value) => HashCode.Combine(hash, value.GetHashCode())),
            values => values.ToArray());
        builder.Property(entity => entity.Weekdays)
            .HasField("_weekdays")
            .HasColumnName("weekdays")
            .HasColumnType("smallint[]")
            .HasConversion(weekdayConverter)
            .Metadata.SetValueComparer(weekdayComparer);

        ValueConverter<IReadOnlyList<int>, short[]> monthDayConverter = new(
            values => values.Select(value => (short)value).ToArray(),
            values => values.Select(value => (int)value).ToArray());
        ValueComparer<IReadOnlyList<int>> monthDayComparer = new(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            values => values.Aggregate(0, (hash, value) => HashCode.Combine(hash, value)),
            values => values.ToArray());
        builder.Property(entity => entity.MonthDays)
            .HasField("_monthDays")
            .HasColumnName("month_days")
            .HasColumnType("smallint[]")
            .HasConversion(monthDayConverter)
            .Metadata.SetValueComparer(monthDayComparer);
        builder.Property(entity => entity.Version).HasColumnName("xmin").IsRowVersion();
        builder.HasOne<TaskTemplate>().WithMany().HasForeignKey(entity => entity.TaskTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.IsActive });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.RecurrenceStartDate });
    }
}

internal sealed class TaskScheduleDateConfiguration : EntityConfigurationBase<TaskScheduleDate>
{
    public override void Configure(EntityTypeBuilder<TaskScheduleDate> builder)
    {
        base.Configure(builder);
        builder.ToTable("task_schedule_dates");
        builder.HasOne<TaskSchedule>().WithMany().HasForeignKey(entity => entity.TaskScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(entity => new { entity.TaskScheduleId, entity.OccurrenceDate }).IsUnique();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.TaskScheduleId });
    }
}

internal sealed class OperationalTaskConfiguration : EntityConfigurationBase<OperationalTask>
{
    public override void Configure(EntityTypeBuilder<OperationalTask> builder)
    {
        base.Configure(builder);
        builder.ToTable("tasks", table =>
            table.HasCheckConstraint("ck_tasks_due_after_start", "\"due_at\" > \"scheduled_start_at\""));
        builder.Property(entity => entity.Title).HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(8000);
        builder.Property(entity => entity.Priority).AsString();
        builder.Property(entity => entity.Status).AsString();
        builder.Property(entity => entity.BlockedReason).HasMaxLength(1000);
        builder.Property(entity => entity.Version).HasColumnName("xmin").IsRowVersion();
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskDistribution>().WithMany().HasForeignKey(entity => entity.TaskDistributionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskTemplate>().WithMany().HasForeignKey(entity => entity.TaskTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskSchedule>().WithMany().HasForeignKey(entity => entity.TaskScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OperationalTask>().WithMany().HasForeignKey(entity => entity.ParentTaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.OccurrenceDate });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.AssigneeUserId, entity.OccurrenceDate });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.AssigneeUserId, entity.Status });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.DepartmentId, entity.Status });
        builder.HasIndex(entity => new { entity.TaskDistributionId, entity.AssigneeUserId })
            .IsUnique()
            .HasFilter("\"task_distribution_id\" IS NOT NULL AND \"assignee_user_id\" IS NOT NULL");
        builder.HasIndex(entity => new { entity.TaskScheduleId, entity.OccurrenceDate, entity.ScheduledStartAt, entity.AssigneeUserId })
            .IsUnique()
            .HasFilter("\"task_schedule_id\" IS NOT NULL AND \"assignee_user_id\" IS NOT NULL");
    }
}

internal sealed class TaskAssignmentHistoryConfiguration : EntityConfigurationBase<TaskAssignmentHistory>
{
    public override void Configure(EntityTypeBuilder<TaskAssignmentHistory> builder)
    {
        base.Configure(builder);
        builder.ToTable("task_assignment_history");
        builder.Property(entity => entity.OccurredAt).IsRequired();
        builder.HasOne<OperationalTask>().WithMany().HasForeignKey(entity => entity.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.PreviousAssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.NewAssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.ChangedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.TaskId, entity.OccurredAt });
    }
}

internal sealed class TaskItemConfiguration : EntityConfigurationBase<TaskItem>
{
    public override void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("task_items");
        builder.Property(entity => entity.Title).HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(4000);
        builder.Property(entity => entity.EvidenceMode).AsString();
        builder.Property(entity => entity.MaxAttachments).HasDefaultValue(5);
        builder.Property(entity => entity.Status).AsString();
        builder.Property(entity => entity.Note).HasMaxLength(2000);
        builder.Property(entity => entity.Version).HasColumnName("xmin").IsRowVersion();
        builder.HasOne<OperationalTask>().WithMany().HasForeignKey(entity => entity.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskTemplateItem>().WithMany().HasForeignKey(entity => entity.TemplateItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.TaskId, entity.SortOrder }).IsUnique();
    }
}

internal sealed class TaskAttachmentConfiguration : EntityConfigurationBase<TaskAttachment>
{
    public override void Configure(EntityTypeBuilder<TaskAttachment> builder)
    {
        base.Configure(builder);
        builder.ToTable("task_attachments");
        builder.Property(entity => entity.FileUrl).HasMaxLength(2048).IsRequired();
        builder.Property(entity => entity.FileType).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.AttachmentType).AsString();
        builder.Property(entity => entity.Caption).HasMaxLength(500);
        builder.HasOne<OperationalTask>().WithMany().HasForeignKey(entity => entity.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskItem>().WithMany().HasForeignKey(entity => entity.TaskItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.TaskId);
    }
}

internal sealed class TaskStatusHistoryConfiguration : EntityConfigurationBase<TaskStatusHistory>
{
    public override void Configure(EntityTypeBuilder<TaskStatusHistory> builder)
    {
        base.Configure(builder);
        builder.ToTable("task_status_history");
        builder.Property(entity => entity.OldStatus).AsNullableString();
        builder.Property(entity => entity.NewStatus).AsString();
        builder.Property(entity => entity.OccurredAt).IsRequired();
        builder.Property(entity => entity.Reason).HasMaxLength(1000);
        builder.HasOne<OperationalTask>().WithMany().HasForeignKey(entity => entity.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.TaskId, entity.OccurredAt });
    }
}
