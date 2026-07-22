using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsManager.Domain.Entities;

namespace OpsManager.Repository.Configurations;

internal sealed class ComplaintConfiguration : EntityConfigurationBase<Complaint>
{
    public override void Configure(EntityTypeBuilder<Complaint> builder)
    {
        base.Configure(builder);
        builder.ToTable("complaints");
        builder.Property(entity => entity.ComplaintNumber).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Title).HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(8000).IsRequired();
        builder.Property(entity => entity.Status).AsString();
        builder.Property(entity => entity.Visibility).AsString();
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.TargetDepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.ComplaintNumber }).IsUnique();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Status, entity.CreatedAt });
    }
}

internal sealed class ComplaintMessageConfiguration : EntityConfigurationBase<ComplaintMessage>
{
    public override void Configure(EntityTypeBuilder<ComplaintMessage> builder)
    {
        base.Configure(builder);
        builder.ToTable("complaint_messages");
        builder.Property(entity => entity.MessageText).HasMaxLength(8000).IsRequired();
        builder.HasOne<Complaint>().WithMany().HasForeignKey(entity => entity.ComplaintId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.ComplaintId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.IsInternal });
    }
}

internal sealed class ComplaintAttachmentConfiguration : EntityConfigurationBase<ComplaintAttachment>
{
    public override void Configure(EntityTypeBuilder<ComplaintAttachment> builder)
    {
        base.Configure(builder);
        builder.ToTable("complaint_attachments");
        builder.Property(entity => entity.FileUrl).HasMaxLength(2048).IsRequired();
        builder.Property(entity => entity.FileType).HasMaxLength(160).IsRequired();
        builder.HasOne<Complaint>().WithMany().HasForeignKey(entity => entity.ComplaintId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ComplaintMessage>().WithMany().HasForeignKey(entity => entity.ComplaintMessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.ComplaintId);
    }
}
