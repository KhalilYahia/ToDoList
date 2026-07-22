using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsManager.Domain.Entities;
using TaskEntity = OpsManager.Domain.Entities.Task;

namespace OpsManager.Repository.Configurations;

internal sealed class OrderTemplateConfiguration : EntityConfigurationBase<OrderTemplate>
{
    public override void Configure(EntityTypeBuilder<OrderTemplate> builder)
    {
        base.Configure(builder);
        builder.ToTable("order_templates", table =>
            table.HasCheckConstraint("ck_order_templates_departments", "\"source_department_id\" <> \"target_department_id\""));
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(4000);
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.SourceDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.TargetDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.BranchId, entity.Name }).IsUnique().HasFilter("\"deleted_at\" IS NULL");
    }
}

internal sealed class OrderTemplateItemConfiguration : EntityConfigurationBase<OrderTemplateItem>
{
    public override void Configure(EntityTypeBuilder<OrderTemplateItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("order_template_items", table =>
        {
            table.HasCheckConstraint("ck_order_template_items_default_quantity", "\"default_quantity\" IS NULL OR \"default_quantity\" >= 0");
            table.HasCheckConstraint("ck_order_template_items_minimum_quantity", "\"minimum_quantity\" IS NULL OR \"minimum_quantity\" >= 0");
            table.HasCheckConstraint("ck_order_template_items_custom_unit", "\"unit_code\" <> 'Custom' OR NULLIF(BTRIM(\"custom_unit_label\"), '') IS NOT NULL");
        });
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(2000);
        builder.Property(entity => entity.UnitCode).AsString();
        builder.Property(entity => entity.CustomUnitLabel).HasMaxLength(80);
        builder.Property(entity => entity.DefaultQuantity).HasPrecision(18, 3);
        builder.Property(entity => entity.MinimumQuantity).HasPrecision(18, 3);
        builder.Property(entity => entity.ImageUrl).HasMaxLength(2048);
        builder.HasOne<OrderTemplate>().WithMany().HasForeignKey(entity => entity.OrderTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrderTemplateId, entity.SortOrder }).IsUnique();
    }
}

internal sealed class DepartmentOrderConfiguration : EntityConfigurationBase<DepartmentOrder>
{
    public override void Configure(EntityTypeBuilder<DepartmentOrder> builder)
    {
        base.Configure(builder);
        builder.ToTable("department_orders", table =>
            table.HasCheckConstraint("ck_department_orders_departments", "\"source_department_id\" <> \"target_department_id\""));
        builder.Property(entity => entity.OrderNumber).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Priority).AsString();
        builder.Property(entity => entity.Status).AsString();
        builder.Property(entity => entity.GeneralNote).HasMaxLength(4000);
        builder.Property(entity => entity.RejectionReason).HasMaxLength(1000);
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderTemplate>().WithMany().HasForeignKey(entity => entity.OrderTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.SourceDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.TargetDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskEntity>().WithMany().HasForeignKey(entity => entity.LinkedTaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.OrderNumber }).IsUnique();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.TargetDepartmentId, entity.Status });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.SourceDepartmentId, entity.CreatedAt });
    }
}

internal sealed class DepartmentOrderItemConfiguration : EntityConfigurationBase<DepartmentOrderItem>
{
    public override void Configure(EntityTypeBuilder<DepartmentOrderItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("department_order_items", table =>
        {
            table.HasCheckConstraint("ck_department_order_items_requested_quantity", "\"requested_quantity\" >= 0");
            table.HasCheckConstraint("ck_department_order_items_fulfilled_quantity", "\"fulfilled_quantity\" >= 0");
            table.HasCheckConstraint("ck_department_order_items_received_quantity", "\"received_quantity\" >= 0");
            table.HasCheckConstraint("ck_department_order_items_custom_unit", "\"unit_code_snapshot\" <> 'Custom' OR NULLIF(BTRIM(\"custom_unit_label_snapshot\"), '') IS NOT NULL");
        });
        builder.Property(entity => entity.ItemNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.ItemDescriptionSnapshot).HasMaxLength(2000);
        builder.Property(entity => entity.UnitCodeSnapshot).AsString();
        builder.Property(entity => entity.CustomUnitLabelSnapshot).HasMaxLength(80);
        builder.Property(entity => entity.RequestedQuantity).HasPrecision(18, 3);
        builder.Property(entity => entity.FulfilledQuantity).HasPrecision(18, 3);
        builder.Property(entity => entity.ReceivedQuantity).HasPrecision(18, 3);
        builder.Property(entity => entity.Status).AsString();
        builder.Property(entity => entity.ItemNote).HasMaxLength(2000);
        builder.Property(entity => entity.FulfillmentNote).HasMaxLength(2000);
        builder.HasOne<DepartmentOrder>().WithMany().HasForeignKey(entity => entity.DepartmentOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderTemplateItem>().WithMany().HasForeignKey(entity => entity.TemplateItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.DepartmentOrderId);
    }
}

internal sealed class DepartmentOrderAttachmentConfiguration : EntityConfigurationBase<DepartmentOrderAttachment>
{
    public override void Configure(EntityTypeBuilder<DepartmentOrderAttachment> builder)
    {
        base.Configure(builder);
        builder.ToTable("department_order_attachments");
        builder.Property(entity => entity.FileUrl).HasMaxLength(2048).IsRequired();
        builder.Property(entity => entity.FileType).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Caption).HasMaxLength(500);
        builder.HasOne<DepartmentOrder>().WithMany().HasForeignKey(entity => entity.DepartmentOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DepartmentOrderItem>().WithMany().HasForeignKey(entity => entity.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.DepartmentOrderId);
    }
}

internal sealed class DepartmentOrderStatusHistoryConfiguration : EntityConfigurationBase<DepartmentOrderStatusHistory>
{
    public override void Configure(EntityTypeBuilder<DepartmentOrderStatusHistory> builder)
    {
        base.Configure(builder);
        builder.ToTable("department_order_status_history");
        builder.Property(entity => entity.OldStatus).AsNullableString();
        builder.Property(entity => entity.NewStatus).AsString();
        builder.Property(entity => entity.Note).HasMaxLength(1000);
        builder.HasOne<DepartmentOrder>().WithMany().HasForeignKey(entity => entity.DepartmentOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.DepartmentOrderId, entity.CreatedAt });
    }
}
