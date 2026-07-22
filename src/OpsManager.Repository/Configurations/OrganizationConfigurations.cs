using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsManager.Domain.Entities;

namespace OpsManager.Repository.Configurations;

internal sealed class OrganizationConfiguration : EntityConfigurationBase<Organization>
{
    public override void Configure(EntityTypeBuilder<Organization> builder)
    {
        base.Configure(builder);
        builder.ToTable("organizations");
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.LegalName).HasMaxLength(240);
        builder.Property(entity => entity.LogoUrl).HasMaxLength(2048);
        builder.Property(entity => entity.Phone).HasMaxLength(40);
        builder.Property(entity => entity.Email).HasMaxLength(320);
        builder.Property(entity => entity.Timezone).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.DefaultLanguage).HasMaxLength(2).IsRequired();
        builder.Property(entity => entity.Status).AsString();
        builder.HasIndex(entity => entity.Name);
        builder.HasIndex(entity => entity.Status);
    }
}

internal sealed class BranchConfiguration : EntityConfigurationBase<Branch>
{
    public override void Configure(EntityTypeBuilder<Branch> builder)
    {
        base.Configure(builder);
        builder.ToTable("branches");
        builder.Property(entity => entity.Name).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Address).HasMaxLength(1000);
        builder.Property(entity => entity.Phone).HasMaxLength(40);
        builder.Property(entity => entity.Timezone).HasMaxLength(100).IsRequired();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Name }).IsUnique().HasFilter("\"deleted_at\" IS NULL");
        builder.HasIndex(entity => new { entity.OrganizationId, entity.IsPrimary }).IsUnique()
            .HasFilter("\"is_primary\" AND \"is_active\" AND \"deleted_at\" IS NULL");
    }
}

internal sealed class DepartmentConfiguration : EntityConfigurationBase<Department>
{
    public override void Configure(EntityTypeBuilder<Department> builder)
    {
        base.Configure(builder);
        builder.ToTable("departments");
        builder.Property(entity => entity.Name).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(2000);
        builder.HasOne<Branch>().WithMany().HasForeignKey(entity => entity.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.SupervisorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.BranchId, entity.Name }).IsUnique().HasFilter("\"deleted_at\" IS NULL");
        builder.HasIndex(entity => new { entity.OrganizationId, entity.IsActive });
    }
}

internal sealed class UserConfiguration : EntityConfigurationBase<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);
        builder.ToTable("users");
        builder.Property(entity => entity.FullName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Email).HasMaxLength(320);
        builder.Property(entity => entity.NormalizedEmail).HasMaxLength(320);
        builder.Property(entity => entity.Phone).HasMaxLength(40);
        builder.Property(entity => entity.PasswordHash).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.ProfileImageUrl).HasMaxLength(2048);
        builder.Property(entity => entity.PreferredLanguage).HasMaxLength(2).IsRequired();
        builder.Property(entity => entity.AccountStatus).AsString();
        builder.HasIndex(entity => entity.NormalizedEmail).IsUnique()
            .HasFilter("\"normalized_email\" IS NOT NULL AND \"deleted_at\" IS NULL");
    }
}

internal sealed class RefreshTokenConfiguration : EntityConfigurationBase<RefreshToken>
{
    public override void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        base.Configure(builder);
        builder.ToTable("refresh_tokens");
        builder.Property(entity => entity.TokenHash).HasMaxLength(512).IsRequired();
        builder.Property(entity => entity.CreatedByIp).HasMaxLength(64);
        builder.Property(entity => entity.RevokedByIp).HasMaxLength(64);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RefreshToken>().WithMany().HasForeignKey(entity => entity.ReplacedByTokenId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.TokenHash).IsUnique();
        builder.HasIndex(entity => new { entity.UserId, entity.ExpiresAt });
    }
}

internal sealed class OrganizationMemberConfiguration : EntityConfigurationBase<OrganizationMember>
{
    public override void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        base.Configure(builder);
        builder.ToTable("organization_members");
        builder.Property(entity => entity.Role).AsString();
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.UserId }).IsUnique();
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Role, entity.IsActive });
    }
}

internal sealed class UserDepartmentConfiguration : EntityConfigurationBase<UserDepartment>
{
    public override void Configure(EntityTypeBuilder<UserDepartment> builder)
    {
        base.Configure(builder);
        builder.ToTable("user_departments");
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(entity => entity.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.UserId, entity.DepartmentId }).IsUnique().HasFilter("\"left_at\" IS NULL");
        builder.HasIndex(entity => new { entity.OrganizationId, entity.DepartmentId });
    }
}
