using OpsManager.Domain.Constants;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Infrastructure;

public sealed class SubscriptionAccessService(IUnitOfWork unitOfWork, IClock clock) : ISubscriptionAccessService
{
    public async Task<SubscriptionAccess> GetAccessAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        OrganizationSubscription? subscription = await unitOfWork.Repository<OrganizationSubscription>()
            .FirstOrDefaultAsync(entity => entity.OrganizationId == organizationId, cancellationToken);
        if (subscription is null)
        {
            return new SubscriptionAccess(SubscriptionAccessMode.Blocked, null, null, "No subscription is configured.");
        }

        DateTimeOffset now = clock.UtcNow;
        return subscription.Status switch
        {
            SubscriptionStatus.Active or SubscriptionStatus.Complimentary =>
                subscription.EndsAt is not null && subscription.EndsAt <= now
                    ? ExpiredAccess(subscription, now)
                    : new SubscriptionAccess(SubscriptionAccessMode.Full, subscription.Status, subscription.EndsAt, null),
            SubscriptionStatus.Trial =>
                subscription.TrialEndsAt is null || subscription.TrialEndsAt > now
                    ? new SubscriptionAccess(SubscriptionAccessMode.Full, subscription.Status, subscription.TrialEndsAt, null)
                    : ExpiredAccess(subscription, now),
            SubscriptionStatus.GracePeriod =>
                subscription.GracePeriodEndsAt is null || subscription.GracePeriodEndsAt > now
                    ? new SubscriptionAccess(SubscriptionAccessMode.GraceLimited, subscription.Status, subscription.GracePeriodEndsAt, "Subscription is in its grace period.")
                    : new SubscriptionAccess(SubscriptionAccessMode.ReadOnly, subscription.Status, subscription.GracePeriodEndsAt, "The grace period has ended."),
            SubscriptionStatus.Expired or SubscriptionStatus.Cancelled =>
                new SubscriptionAccess(SubscriptionAccessMode.ReadOnly, subscription.Status, subscription.EndsAt, "The organization is read-only."),
            SubscriptionStatus.Suspended =>
                new SubscriptionAccess(SubscriptionAccessMode.Blocked, subscription.Status, subscription.EndsAt, subscription.SuspensionReason ?? "The subscription is suspended."),
            _ => new SubscriptionAccess(SubscriptionAccessMode.Blocked, subscription.Status, subscription.EndsAt, "Subscription access is blocked."),
        };
    }

    public async Task EnsureWriteAllowedAsync(
        Guid organizationId,
        string? featureKey = null,
        CancellationToken cancellationToken = default)
    {
        SubscriptionAccess access = await GetAccessAsync(organizationId, cancellationToken);
        if (access.Mode is SubscriptionAccessMode.ReadOnly or SubscriptionAccessMode.Blocked)
        {
            throw new SubscriptionRestrictionException(access.Reason ?? "The subscription does not allow changes.");
        }

        await EnsureFeatureEnabledAsync(organizationId, featureKey, cancellationToken);
    }

    public async Task EnsureReadAllowedAsync(
        Guid organizationId,
        string? featureKey = null,
        CancellationToken cancellationToken = default)
    {
        SubscriptionAccess access = await GetAccessAsync(organizationId, cancellationToken);
        if (access.Mode == SubscriptionAccessMode.Blocked)
        {
            throw new SubscriptionRestrictionException(access.Reason ?? "The subscription does not allow access.");
        }

        await EnsureFeatureEnabledAsync(organizationId, featureKey, cancellationToken);
    }

    private async Task EnsureFeatureEnabledAsync(
        Guid organizationId,
        string? featureKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            return;
        }

        OrganizationSubscription? subscription = await unitOfWork.Repository<OrganizationSubscription>()
            .FirstOrDefaultAsync(entity => entity.OrganizationId == organizationId, cancellationToken);
        SubscriptionPlan? plan = subscription is null
            ? null
            : await unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(subscription.PlanId, cancellationToken);
        if (plan is null || !plan.Features.TryGetValue(featureKey, out string? enabled) ||
            !bool.TryParse(enabled, out bool isEnabled) || !isEnabled)
        {
            throw new SubscriptionRestrictionException($"The '{featureKey}' feature is not enabled for this organization.", "feature_not_enabled");
        }
    }

    private static SubscriptionAccess ExpiredAccess(OrganizationSubscription subscription, DateTimeOffset now)
    {
        if (subscription.GracePeriodEndsAt is not null && subscription.GracePeriodEndsAt > now)
        {
            return new SubscriptionAccess(SubscriptionAccessMode.GraceLimited, subscription.Status, subscription.GracePeriodEndsAt, "Subscription is in its grace period.");
        }

        return new SubscriptionAccess(SubscriptionAccessMode.ReadOnly, subscription.Status, subscription.EndsAt ?? subscription.TrialEndsAt, "The organization is read-only.");
    }
}
