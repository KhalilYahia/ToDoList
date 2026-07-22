using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsManager.Domain.Entities;

namespace OpsManager.Repository.Configurations;

internal sealed class SubscriptionPlanConfiguration : EntityConfigurationBase<SubscriptionPlan>
{
    public override void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        base.Configure(builder);
        builder.ToTable("subscription_plans", table =>
        {
            table.HasCheckConstraint("ck_subscription_plans_limits", "\"max_users\" > 0 AND \"max_branches\" > 0 AND \"max_storage_mb\" >= 0");
            table.HasCheckConstraint("ck_subscription_plans_prices", "(\"monthly_price\" IS NULL OR \"monthly_price\" >= 0) AND (\"yearly_price\" IS NULL OR \"yearly_price\" >= 0)");
        });
        builder.Property(entity => entity.Name).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Code).HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(4000);
        builder.Property(entity => entity.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(entity => entity.YearlyPrice).HasPrecision(18, 2);
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.HasJsonDictionary(entity => entity.Features);
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => entity.IsActive);
    }
}

internal sealed class OrganizationSubscriptionConfiguration : EntityConfigurationBase<OrganizationSubscription>
{
    public override void Configure(EntityTypeBuilder<OrganizationSubscription> builder)
    {
        base.Configure(builder);
        builder.ToTable("organization_subscriptions", table =>
        {
            table.HasCheckConstraint("ck_organization_subscriptions_period", "\"ends_at\" IS NULL OR \"starts_at\" IS NULL OR \"ends_at\" >= \"starts_at\"");
            table.HasCheckConstraint("ck_organization_subscriptions_trial", "\"trial_ends_at\" IS NULL OR \"trial_started_at\" IS NULL OR \"trial_ends_at\" > \"trial_started_at\"");
        });
        builder.Property(entity => entity.Status).AsString();
        builder.Property(entity => entity.BillingMode).AsString();
        builder.Property(entity => entity.SuspensionReason).HasMaxLength(1000);
        builder.Property(entity => entity.Notes).HasMaxLength(4000);
        builder.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(entity => entity.PlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlatformUser>().WithMany().HasForeignKey(entity => entity.ActivatedByPlatformUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.Status });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.EndsAt });
    }
}

internal sealed class SubscriptionHistoryConfiguration : EntityConfigurationBase<SubscriptionHistory>
{
    public override void Configure(EntityTypeBuilder<SubscriptionHistory> builder)
    {
        base.Configure(builder);
        builder.ToTable("subscription_history");
        builder.Property(entity => entity.OldStatus).AsNullableString();
        builder.Property(entity => entity.NewStatus).AsString();
        builder.Property(entity => entity.ActionType).AsString();
        builder.Property(entity => entity.Reason).HasMaxLength(1000);
        builder.HasOne<OrganizationSubscription>().WithMany().HasForeignKey(entity => entity.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlatformUser>().WithMany().HasForeignKey(entity => entity.ChangedByPlatformUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.SubscriptionId, entity.CreatedAt });
    }
}

internal sealed class ManualPaymentConfiguration : EntityConfigurationBase<ManualPayment>
{
    public override void Configure(EntityTypeBuilder<ManualPayment> builder)
    {
        base.Configure(builder);
        builder.ToTable("manual_payments", table =>
        {
            table.HasCheckConstraint("ck_manual_payments_amount", "\"amount\" >= 0");
            table.HasCheckConstraint("ck_manual_payments_period", "\"period_end\" >= \"period_start\"");
        });
        builder.Property(entity => entity.Amount).HasPrecision(18, 2);
        builder.Property(entity => entity.Currency).HasMaxLength(3).IsRequired();
        builder.Property(entity => entity.PaymentMethod).AsString();
        builder.Property(entity => entity.PaymentStatus).AsString();
        builder.Property(entity => entity.PaymentReference).HasMaxLength(200);
        builder.Property(entity => entity.ReceiptFileUrl).HasMaxLength(2048);
        builder.Property(entity => entity.Note).HasMaxLength(2000);
        builder.HasOne<OrganizationSubscription>().WithMany().HasForeignKey(entity => entity.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlatformUser>().WithMany().HasForeignKey(entity => entity.RecordedByPlatformUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.PaymentStatus, entity.PaidAt });
    }
}

internal sealed class PlatformUserConfiguration : EntityConfigurationBase<PlatformUser>
{
    public override void Configure(EntityTypeBuilder<PlatformUser> builder)
    {
        base.Configure(builder);
        builder.ToTable("platform_users");
        builder.Property(entity => entity.FullName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Email).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(entity => entity.PasswordHash).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.Role).AsString();
        builder.Property(entity => entity.Status).AsString();
        builder.Property(entity => entity.PreferredLanguage).HasMaxLength(2).IsRequired();
        builder.HasIndex(entity => entity.NormalizedEmail).IsUnique().HasFilter("\"deleted_at\" IS NULL");
    }
}

internal sealed class NotificationConfiguration : EntityConfigurationBase<Notification>
{
    public override void Configure(EntityTypeBuilder<Notification> builder)
    {
        base.Configure(builder);
        builder.ToTable("notifications");
        builder.Property(entity => entity.NotificationType).AsString();
        builder.HasJsonDictionary(entity => entity.Parameters);
        builder.Property(entity => entity.Title).HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Body).HasMaxLength(4000).IsRequired();
        builder.Property(entity => entity.RelatedEntityType).HasMaxLength(160);
        builder.HasOne<User>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.UserId, entity.IsRead, entity.CreatedAt });
    }
}

internal sealed class AuditLogConfiguration : EntityConfigurationBase<AuditLog>
{
    public override void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        base.Configure(builder);
        builder.ToTable("audit_logs");
        builder.Property(entity => entity.Action).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.EntityType).HasMaxLength(160).IsRequired();
        builder.HasJsonDictionary(entity => entity.OldValues);
        builder.HasJsonDictionary(entity => entity.NewValues);
        builder.Property(entity => entity.IpAddress).HasMaxLength(64);
        builder.Property(entity => entity.UserAgent).HasMaxLength(1000);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.EntityType, entity.EntityId });
        builder.HasIndex(entity => new { entity.OrganizationId, entity.CreatedAt });
    }
}

internal sealed class PlatformAuditLogConfiguration : EntityConfigurationBase<PlatformAuditLog>
{
    public override void Configure(EntityTypeBuilder<PlatformAuditLog> builder)
    {
        base.Configure(builder);
        builder.ToTable("platform_audit_logs");
        builder.Property(entity => entity.Action).HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.EntityType).HasMaxLength(160).IsRequired();
        builder.HasJsonDictionary(entity => entity.OldValues);
        builder.HasJsonDictionary(entity => entity.NewValues);
        builder.Property(entity => entity.IpAddress).HasMaxLength(64);
        builder.Property(entity => entity.UserAgent).HasMaxLength(1000);
        builder.HasOne<PlatformUser>().WithMany().HasForeignKey(entity => entity.ActorPlatformUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrganizationId, entity.CreatedAt });
    }
}
