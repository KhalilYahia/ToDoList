using OpsManager.Domain.Enums;
using OpsManager.Service.Common;

namespace OpsManager.Service.Platform.DTOs;

public sealed record PlatformLoginRequest(string Email, string Password);
public sealed record PlatformUserDto(
    Guid Id,
    string FullName,
    string Email,
    PlatformRole Role,
    string PreferredLanguage);
public sealed record PlatformAuthenticationResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    PlatformUserDto User);
public sealed record PlatformAuthenticationSession(
    PlatformAuthenticationResponse Response,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record SaveSubscriptionPlanRequest(
    string Name,
    string Code,
    string? Description,
    decimal? MonthlyPrice,
    decimal? YearlyPrice,
    string Currency,
    int MaxUsers,
    int MaxBranches,
    int MaxStorageMb,
    IReadOnlyDictionary<string, string> Features,
    int GracePeriodDays,
    bool IsActive);

public sealed record SubscriptionPlanDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    decimal? MonthlyPrice,
    decimal? YearlyPrice,
    string Currency,
    int MaxUsers,
    int MaxBranches,
    int MaxStorageMb,
    IReadOnlyDictionary<string, string> Features,
    int GracePeriodDays,
    bool IsActive);

public sealed record PlatformOrganizationDto(
    Guid Id,
    string Name,
    string? LegalName,
    string Timezone,
    OrganizationStatus Status,
    SubscriptionStatus? SubscriptionStatus,
    DateTimeOffset? SubscriptionEndsAt);

public sealed record OrganizationSubscriptionDto(
    Guid Id,
    Guid OrganizationId,
    Guid PlanId,
    SubscriptionStatus Status,
    BillingMode BillingMode,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? GracePeriodEndsAt,
    string? SuspensionReason,
    string? Notes);

public sealed record ActivateSubscriptionRequest(
    Guid PlanId,
    BillingMode BillingMode,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    bool Complimentary,
    string? Reason);
public sealed record ExtendSubscriptionRequest(DateTimeOffset EndsAt, string? Reason);
public sealed record ChangeSubscriptionPlanRequest(Guid PlanId, string? Reason);
public sealed record SuspendSubscriptionRequest(string Reason);
public sealed record SubscriptionReasonRequest(string? Reason);

public sealed record RecordManualPaymentRequest(
    Guid OrganizationId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    string? PaymentReference,
    DateTimeOffset? PaidAt,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string? ReceiptFileUrl,
    string? Note,
    bool ActivateSubscription,
    Guid? ActivationPlanId,
    DateTimeOffset? ActivationEndsAt);

public sealed record ManualPaymentDto(
    Guid Id,
    Guid OrganizationId,
    Guid SubscriptionId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    string? PaymentReference,
    PaymentStatus Status,
    DateTimeOffset? PaidAt,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string? ReceiptFileUrl,
    string? Note);
