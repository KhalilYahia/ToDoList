using OpsManager.Domain.Common;
using OpsManager.Domain.Constants;
using OpsManager.Domain.Enums;

namespace OpsManager.Domain.Entities;

public sealed class SubscriptionPlan : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? MonthlyPrice { get; set; }
    public decimal? YearlyPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int MaxUsers { get; set; }
    public int MaxBranches { get; set; }
    public int MaxStorageMb { get; set; }
    public Dictionary<string, string> Features { get; set; } = new(StringComparer.Ordinal);
    public int GracePeriodDays { get; set; } = 7;
    public bool IsActive { get; set; } = true;
}

public sealed class OrganizationSubscription : TenantAuditableEntity
{
    public Guid PlanId { get; set; }
    public SubscriptionStatus Status { get; set; }
    public BillingMode BillingMode { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public DateTimeOffset? TrialStartedAt { get; set; }
    public DateTimeOffset? TrialEndsAt { get; set; }
    public DateTimeOffset? GracePeriodEndsAt { get; set; }
    public Guid? ActivatedByPlatformUserId { get; set; }
    public DateTimeOffset? SuspendedAt { get; set; }
    public Guid? SuspendedByPlatformUserId { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? Notes { get; set; }
}

public sealed class SubscriptionHistory : TenantAuditableEntity
{
    public Guid SubscriptionId { get; set; }
    public SubscriptionStatus? OldStatus { get; set; }
    public SubscriptionStatus NewStatus { get; set; }
    public DateTimeOffset? OldEndsAt { get; set; }
    public DateTimeOffset? NewEndsAt { get; set; }
    public SubscriptionActionType ActionType { get; set; }
    public Guid? ChangedByPlatformUserId { get; set; }
    public string? Reason { get; set; }
}

public sealed class ManualPayment : TenantAuditableEntity
{
    public Guid SubscriptionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentMethod PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public Guid RecordedByPlatformUserId { get; set; }
    public string? ReceiptFileUrl { get; set; }
    public string? Note { get; set; }
}

public sealed class PlatformUser : SoftDeletableEntity
{
    private PlatformUser() { }

    public PlatformUser(string fullName, string email, string passwordHash, PlatformRole role, string preferredLanguage)
    {
        FullName = Guard.Required(fullName, nameof(fullName), 200);
        Email = Guard.Required(email, nameof(email), 320);
        NormalizedEmail = Email.ToUpperInvariant();
        PasswordHash = Guard.Required(passwordHash, nameof(passwordHash), 1000);
        Role = role;
        Guard.SupportedLanguage(preferredLanguage, nameof(preferredLanguage));
        PreferredLanguage = preferredLanguage;
    }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public PlatformRole Role { get; set; }
    public UserAccountStatus Status { get; set; } = UserAccountStatus.Active;
    public string PreferredLanguage { get; set; } = SupportedLanguages.English;
    public DateTimeOffset? LastLoginAt { get; set; }
}

public sealed class Notification : TenantAuditableEntity
{
    public Guid UserId { get; set; }
    public NotificationType NotificationType { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class AuditLog : TenantAuditableEntity
{
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public Dictionary<string, string> OldValues { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> NewValues { get; set; } = new(StringComparer.Ordinal);
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public sealed class PlatformAuditLog : AuditableEntity
{
    public Guid? ActorPlatformUserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public Dictionary<string, string> OldValues { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> NewValues { get; set; } = new(StringComparer.Ordinal);
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
