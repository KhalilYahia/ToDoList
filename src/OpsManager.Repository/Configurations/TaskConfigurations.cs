using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsManager.Domain.Entities;
using TaskEntity = OpsManager.Domain.Entities.Task;

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
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.DefaultDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.DefaultAssigneeUserId).OnDelete(DeleteBehavior.Restrict);
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
        builder.HasOne<TaskTemplate>().WithMany().HasForeignKey(entity => entity.TaskTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.TaskTemplateId, entity.SortOrder }).IsUnique();
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

internal sealed class TaskScheduleConfiguration : EntityConfigurationBase<TaskSchedule>
{
    public override void Configure(EntityTypeBuilder<TaskSchedule> builder)
    {
        base.Configure(builder);
        builder.ToTable("task_schedules", table =>
        {
            table.HasCheckConstraint("ck_task_schedules_interval", "\"recurrence_interval\" > 0");
            table.HasCheckConstraint("ck_task_schedules_dates", "\"end_date\" IS NULL OR \"end_date\" >= \"start_date\"");
            table.HasCheckConstraint("ck_task_schedules_month_day", "\"month_day\" IS NULL OR (\"month_day\" >= 1 AND \"month_day\" <= 31)");
        });
        builder.Property(entity => entity.RecurrenceType).AsString();
        builder.Property(entity => entity.Weekdays).HasColumnType("integer[]");
        builder.Property(entity => entity.RecurrenceRule).HasMaxLength(1000);
        builder.HasOne<TaskTemplate>().WithMany().HasForeignKey(entity => entity.TaskTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.IsActive });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.StartDate });
    }
}

internal sealed class TaskConfiguration : EntityConfigurationBase<TaskEntity>
{
    public override void Configure(EntityTypeBuilder<TaskEntity> builder)
    {
        base.Configure(builder);
        builder.ToTable("tasks", table =>
            table.HasCheckConstraint("ck_tasks_due_after_start", "\"due_at\" > \"scheduled_start_at\""));
        builder.Property(entity => entity.Title).HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(8000);
        builder.Property(entity => entity.Priority).AsString();
        builder.Property(entity => entity.Status).AsString();
        builder.Property(entity => entity.BlockedReason).HasMaxLength(1000);
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskTemplate>().WithMany().HasForeignKey(entity => entity.TaskTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskSchedule>().WithMany().HasForeignKey(entity => entity.TaskScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskEntity>().WithMany().HasForeignKey(entity => entity.ParentTaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.OccurrenceDate });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.AssigneeUserId, entity.Status });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.DepartmentId, entity.Status });
        builder.HasIndex(entity => new { entity.TaskScheduleId, entity.OccurrenceDate, entity.ScheduledStartAt })
            .IsUnique()
            .HasFilter("\"task_schedule_id\" IS NOT NULL");
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
        builder.Property(entity => entity.Status).AsString();
        builder.Property(entity => entity.Note).HasMaxLength(2000);
        builder.HasOne<TaskEntity>().WithMany().HasForeignKey(entity => entity.TaskId).OnDelete(DeleteBehavior.Restrict);
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
        builder.HasOne<TaskEntity>().WithMany().HasForeignKey(entity => entity.TaskId).OnDelete(DeleteBehavior.Restrict);
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
        builder.Property(entity => entity.Reason).HasMaxLength(1000);
        builder.HasOne<TaskEntity>().WithMany().HasForeignKey(entity => entity.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.TaskId, entity.CreatedAt });
    }
}
