using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Platform.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Platform;

public enum SubscriptionOperation { Activate, Extend, ChangePlan, Suspend, Reactivate, Expire }
public enum PaymentOperation { Confirm, Reject, Refund }

public interface IPlatformAdministrationService
{
    Task<PagedResponse<SubscriptionPlanDto>> ListPlansAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task<SubscriptionPlanDto> CreatePlanAsync(SaveSubscriptionPlanRequest request, CancellationToken cancellationToken = default);
    Task<SubscriptionPlanDto> UpdatePlanAsync(Guid id, SaveSubscriptionPlanRequest request, CancellationToken cancellationToken = default);
    Task SetPlanActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default);
    Task<PagedResponse<PlatformOrganizationDto>> ListOrganizationsAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task<PlatformOrganizationDto> GetOrganizationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrganizationSubscriptionDto> GetSubscriptionAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<OrganizationSubscriptionDto> ActivateAsync(
        Guid organizationId,
        ActivateSubscriptionRequest request,
        CancellationToken cancellationToken = default);
    Task<OrganizationSubscriptionDto> ExtendAsync(
        Guid organizationId,
        ExtendSubscriptionRequest request,
        CancellationToken cancellationToken = default);
    Task<OrganizationSubscriptionDto> ChangePlanAsync(
        Guid organizationId,
        ChangeSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default);
    Task<OrganizationSubscriptionDto> ChangeStatusAsync(
        Guid organizationId,
        SubscriptionOperation operation,
        string? reason,
        CancellationToken cancellationToken = default);
    Task<PagedResponse<ManualPaymentDto>> ListPaymentsAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task<ManualPaymentDto> GetPaymentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ManualPaymentDto> RecordPaymentAsync(RecordManualPaymentRequest request, CancellationToken cancellationToken = default);
    Task<ManualPaymentDto> ChangePaymentStatusAsync(
        Guid id,
        PaymentOperation operation,
        CancellationToken cancellationToken = default);
}

public interface ISubscriptionLifecycleService
{
    Task<int> ProcessExpirationsAsync(CancellationToken cancellationToken = default);
}

public sealed class PlatformAdministrationService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    IClock clock,
    IAuditService auditService) : IPlatformAdministrationService
{
    public async Task<PagedResponse<SubscriptionPlanDto>> ListPlansAsync(
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformUser();
        PagedResult<SubscriptionPlan> result =
            await unitOfWork.Repository<SubscriptionPlan>().ListAsync(null, page.ToDomain(), cancellationToken);
        return PagedResponse.Map(result, Map);
    }

    public async Task<SubscriptionPlanDto> CreatePlanAsync(
        SaveSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        ValidatePlan(request);
        string code = request.Code.Trim().ToLowerInvariant();
        if (await unitOfWork.Repository<SubscriptionPlan>().AnyAsync(plan => plan.Code == code, cancellationToken))
        {
            throw new ConflictException("A plan with this code already exists.", "duplicate_plan_code");
        }

        SubscriptionPlan plan = new();
        Apply(plan, request, code);
        await unitOfWork.Repository<SubscriptionPlan>().AddAsync(plan, cancellationToken);
        await auditService.RecordPlatformAsync(
            "subscription-plan.created",
            nameof(SubscriptionPlan),
            plan.Id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(plan);
    }

    public async Task<SubscriptionPlanDto> UpdatePlanAsync(
        Guid id,
        SaveSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        ValidatePlan(request);
        SubscriptionPlan plan = await unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(SubscriptionPlan));
        string code = request.Code.Trim().ToLowerInvariant();
        bool duplicate = await unitOfWork.Repository<SubscriptionPlan>()
            .AnyAsync(entity => entity.Code == code && entity.Id != id, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("A plan with this code already exists.", "duplicate_plan_code");
        }

        Apply(plan, request, code);
        unitOfWork.Repository<SubscriptionPlan>().Update(plan);
        await auditService.RecordPlatformAsync(
            "subscription-plan.updated",
            nameof(SubscriptionPlan),
            plan.Id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(plan);
    }

    public async Task SetPlanActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        _ = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        SubscriptionPlan plan = await unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(SubscriptionPlan));
        plan.IsActive = active;
        unitOfWork.Repository<SubscriptionPlan>().Update(plan);
        await auditService.RecordPlatformAsync(
            active ? "subscription-plan.activated" : "subscription-plan.deactivated",
            nameof(SubscriptionPlan),
            plan.Id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResponse<PlatformOrganizationDto>> ListOrganizationsAsync(
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformUser();
        PagedResult<Organization> result =
            await unitOfWork.Repository<Organization>().ListAsync(null, page.ToDomain(), cancellationToken);
        List<PlatformOrganizationDto> items = [];
        foreach (Organization organization in result.Items)
        {
            items.Add(await MapOrganizationAsync(organization, cancellationToken));
        }

        return new PagedResponse<PlatformOrganizationDto>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<PlatformOrganizationDto> GetOrganizationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequirePlatformUser();
        Organization organization = await unitOfWork.Repository<Organization>().GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Organization));
        return await MapOrganizationAsync(organization, cancellationToken);
    }

    public async Task<OrganizationSubscriptionDto> GetSubscriptionAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformUser();
        return Map(await GetSubscriptionEntityAsync(organizationId, cancellationToken));
    }

    public async Task<OrganizationSubscriptionDto> ActivateAsync(
        Guid organizationId,
        ActivateSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid actorId = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        SubscriptionPlan plan = await GetActivePlanAsync(request.PlanId, cancellationToken);
        OrganizationSubscription subscription = await GetSubscriptionEntityAsync(organizationId, cancellationToken);
        EnsureTransition(subscription.Status, request.Complimentary ? SubscriptionStatus.Complimentary : SubscriptionStatus.Active);
        SubscriptionStatus oldStatus = subscription.Status;
        DateTimeOffset? oldEnd = subscription.EndsAt;
        subscription.PlanId = plan.Id;
        subscription.Status = request.Complimentary ? SubscriptionStatus.Complimentary : SubscriptionStatus.Active;
        subscription.BillingMode = request.Complimentary ? BillingMode.Manual : request.BillingMode;
        subscription.StartsAt = request.StartsAt;
        subscription.EndsAt = request.EndsAt;
        subscription.GracePeriodEndsAt = request.EndsAt?.AddDays(plan.GracePeriodDays);
        subscription.ActivatedByPlatformUserId = actorId;
        subscription.SuspendedAt = null;
        subscription.SuspendedByPlatformUserId = null;
        subscription.SuspensionReason = null;
        unitOfWork.Repository<OrganizationSubscription>().Update(subscription);
        await RecordHistoryAsync(subscription, oldStatus, oldEnd, SubscriptionActionType.Activated, request.Reason, actorId, cancellationToken);
        await AuditSubscriptionAsync(subscription, "subscription.activated", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(subscription);
    }

    public async Task<OrganizationSubscriptionDto> ExtendAsync(
        Guid organizationId,
        ExtendSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid actorId = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        OrganizationSubscription subscription = await GetSubscriptionEntityAsync(organizationId, cancellationToken);
        if (request.EndsAt <= clock.UtcNow)
        {
            throw Validation(nameof(request.EndsAt), "The new end date must be in the future.");
        }

        SubscriptionPlan plan = await unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(subscription.PlanId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(SubscriptionPlan));
        DateTimeOffset? oldEnd = subscription.EndsAt;
        SubscriptionStatus oldStatus = subscription.Status;
        subscription.EndsAt = request.EndsAt;
        subscription.GracePeriodEndsAt = request.EndsAt.AddDays(plan.GracePeriodDays);
        if (subscription.Status is SubscriptionStatus.Expired or SubscriptionStatus.GracePeriod)
        {
            subscription.Status = SubscriptionStatus.Active;
        }

        unitOfWork.Repository<OrganizationSubscription>().Update(subscription);
        await RecordHistoryAsync(subscription, oldStatus, oldEnd, SubscriptionActionType.Extended, request.Reason, actorId, cancellationToken);
        await AuditSubscriptionAsync(subscription, "subscription.extended", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(subscription);
    }

    public async Task<OrganizationSubscriptionDto> ChangePlanAsync(
        Guid organizationId,
        ChangeSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid actorId = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        _ = await GetActivePlanAsync(request.PlanId, cancellationToken);
        OrganizationSubscription subscription = await GetSubscriptionEntityAsync(organizationId, cancellationToken);
        subscription.PlanId = request.PlanId;
        unitOfWork.Repository<OrganizationSubscription>().Update(subscription);
        await RecordHistoryAsync(
            subscription,
            subscription.Status,
            subscription.EndsAt,
            SubscriptionActionType.Activated,
            request.Reason ?? "Plan changed.",
            actorId,
            cancellationToken);
        await AuditSubscriptionAsync(subscription, "subscription.plan-changed", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(subscription);
    }

    public async Task<OrganizationSubscriptionDto> ChangeStatusAsync(
        Guid organizationId,
        SubscriptionOperation operation,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        Guid actorId = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        OrganizationSubscription subscription = await GetSubscriptionEntityAsync(organizationId, cancellationToken);
        SubscriptionStatus target = operation switch
        {
            SubscriptionOperation.Suspend => SubscriptionStatus.Suspended,
            SubscriptionOperation.Reactivate => SubscriptionStatus.Active,
            SubscriptionOperation.Expire => SubscriptionStatus.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        EnsureTransition(subscription.Status, target);
        SubscriptionStatus oldStatus = subscription.Status;
        subscription.Status = target;
        SubscriptionActionType action = operation switch
        {
            SubscriptionOperation.Suspend => SubscriptionActionType.Suspended,
            SubscriptionOperation.Reactivate => SubscriptionActionType.Reactivated,
            SubscriptionOperation.Expire => SubscriptionActionType.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        if (target == SubscriptionStatus.Suspended)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw Validation(nameof(reason), "A suspension reason is required.");
            }

            subscription.SuspendedAt = clock.UtcNow;
            subscription.SuspendedByPlatformUserId = actorId;
            subscription.SuspensionReason = reason.Trim();
        }
        else if (target == SubscriptionStatus.Active)
        {
            subscription.SuspendedAt = null;
            subscription.SuspendedByPlatformUserId = null;
            subscription.SuspensionReason = null;
        }

        unitOfWork.Repository<OrganizationSubscription>().Update(subscription);
        await RecordHistoryAsync(subscription, oldStatus, subscription.EndsAt, action, reason, actorId, cancellationToken);
        await AuditSubscriptionAsync(subscription, $"subscription.{operation.ToString().ToLowerInvariant()}", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(subscription);
    }

    public async Task<PagedResponse<ManualPaymentDto>> ListPaymentsAsync(
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformUser();
        PagedResult<ManualPayment> result =
            await unitOfWork.Repository<ManualPayment>().ListAsync(null, page.ToDomain(), cancellationToken);
        return PagedResponse.Map(result, Map);
    }

    public async Task<ManualPaymentDto> GetPaymentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequirePlatformUser();
        return Map(await unitOfWork.Repository<ManualPayment>().GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(ManualPayment)));
    }

    public async Task<ManualPaymentDto> RecordPaymentAsync(
        RecordManualPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid actorId = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        if (request.Amount < 0 || request.PeriodEnd < request.PeriodStart ||
            request.Currency.Trim().Length != 3)
        {
            throw Validation(nameof(request), "Amount, currency, or payment period is invalid.");
        }

        OrganizationSubscription subscription = await GetSubscriptionEntityAsync(request.OrganizationId, cancellationToken);
        await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            ManualPayment payment = new()
            {
                OrganizationId = request.OrganizationId,
                SubscriptionId = subscription.Id,
                Amount = request.Amount,
                Currency = request.Currency.Trim().ToUpperInvariant(),
                PaymentMethod = request.PaymentMethod,
                PaymentReference = request.PaymentReference?.Trim(),
                PaymentStatus = PaymentStatus.Pending,
                PaidAt = request.PaidAt,
                PeriodStart = request.PeriodStart,
                PeriodEnd = request.PeriodEnd,
                RecordedByPlatformUserId = actorId,
                ReceiptFileUrl = request.ReceiptFileUrl?.Trim(),
                Note = request.Note?.Trim(),
            };
            await unitOfWork.Repository<ManualPayment>().AddAsync(payment, cancellationToken);
            if (request.ActivateSubscription)
            {
                if (!request.ActivationPlanId.HasValue || !request.ActivationEndsAt.HasValue)
                {
                    throw Validation(nameof(request.ActivationPlanId), "Activation plan and end date are required.");
                }

                SubscriptionPlan plan = await GetActivePlanAsync(request.ActivationPlanId.Value, cancellationToken);
                SubscriptionStatus oldStatus = subscription.Status;
                subscription.PlanId = plan.Id;
                subscription.Status = SubscriptionStatus.Active;
                subscription.BillingMode = BillingMode.Manual;
                subscription.StartsAt = clock.UtcNow;
                subscription.EndsAt = request.ActivationEndsAt;
                subscription.GracePeriodEndsAt = request.ActivationEndsAt.Value.AddDays(plan.GracePeriodDays);
                subscription.ActivatedByPlatformUserId = actorId;
                unitOfWork.Repository<OrganizationSubscription>().Update(subscription);
                await RecordHistoryAsync(
                    subscription,
                    oldStatus,
                    null,
                    SubscriptionActionType.Activated,
                    "Activated with manual payment record.",
                    actorId,
                    cancellationToken);
            }

            await auditService.RecordPlatformAsync(
                "manual-payment.recorded",
                nameof(ManualPayment),
                payment.Id,
                request.OrganizationId,
                cancellationToken: cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(payment);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ManualPaymentDto> ChangePaymentStatusAsync(
        Guid id,
        PaymentOperation operation,
        CancellationToken cancellationToken = default)
    {
        _ = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        ManualPayment payment = await unitOfWork.Repository<ManualPayment>().GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(ManualPayment));
        PaymentStatus target = operation switch
        {
            PaymentOperation.Confirm => PaymentStatus.Confirmed,
            PaymentOperation.Reject => PaymentStatus.Rejected,
            PaymentOperation.Refund => PaymentStatus.Refunded,
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        bool valid = (payment.PaymentStatus, target) switch
        {
            (PaymentStatus.Pending, PaymentStatus.Confirmed) => true,
            (PaymentStatus.Pending, PaymentStatus.Rejected) => true,
            (PaymentStatus.Confirmed, PaymentStatus.Refunded) => true,
            _ => false,
        };
        if (!valid)
        {
            throw new ConflictException("The payment status transition is invalid.", "invalid_payment_transition");
        }

        payment.PaymentStatus = target;
        unitOfWork.Repository<ManualPayment>().Update(payment);
        await auditService.RecordPlatformAsync(
            $"manual-payment.{operation.ToString().ToLowerInvariant()}",
            nameof(ManualPayment),
            payment.Id,
            payment.OrganizationId,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(payment);
    }

    private async Task<PlatformOrganizationDto> MapOrganizationAsync(
        Organization organization,
        CancellationToken cancellationToken)
    {
        OrganizationSubscription? subscription = await unitOfWork.Repository<OrganizationSubscription>()
            .FirstOrDefaultAsync(item => item.OrganizationId == organization.Id, cancellationToken);
        return new PlatformOrganizationDto(
            organization.Id,
            organization.Name,
            organization.LegalName,
            organization.Timezone,
            organization.Status,
            subscription?.Status,
            subscription?.EndsAt ?? subscription?.TrialEndsAt);
    }

    private async Task<OrganizationSubscription> GetSubscriptionEntityAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await unitOfWork.Repository<OrganizationSubscription>()
            .FirstOrDefaultAsync(subscription => subscription.OrganizationId == organizationId, cancellationToken)
        ?? throw new EntityNotFoundException(nameof(OrganizationSubscription));

    private async Task<SubscriptionPlan> GetActivePlanAsync(Guid id, CancellationToken cancellationToken)
    {
        SubscriptionPlan plan = await unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(SubscriptionPlan));
        if (!plan.IsActive)
        {
            throw new ConflictException("The subscription plan is inactive.", "inactive_plan");
        }

        return plan;
    }

    private async Task RecordHistoryAsync(
        OrganizationSubscription subscription,
        SubscriptionStatus oldStatus,
        DateTimeOffset? oldEndsAt,
        SubscriptionActionType action,
        string? reason,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        await unitOfWork.Repository<SubscriptionHistory>().AddAsync(
            new SubscriptionHistory
            {
                OrganizationId = subscription.OrganizationId,
                SubscriptionId = subscription.Id,
                OldStatus = oldStatus,
                NewStatus = subscription.Status,
                OldEndsAt = oldEndsAt,
                NewEndsAt = subscription.EndsAt,
                ActionType = action,
                ChangedByPlatformUserId = actorId,
                Reason = reason?.Trim(),
            },
            cancellationToken);
    }

    private Task AuditSubscriptionAsync(
        OrganizationSubscription subscription,
        string action,
        CancellationToken cancellationToken) =>
        auditService.RecordPlatformAsync(
            action,
            nameof(OrganizationSubscription),
            subscription.Id,
            subscription.OrganizationId,
            cancellationToken: cancellationToken);

    private void RequirePlatformUser()
    {
        if (!currentUser.IsAuthenticated || currentUser.PlatformUserId is null)
        {
            throw new ForbiddenAccessException("Platform access is required.");
        }
    }

    private static void ValidatePlan(SaveSubscriptionPlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Code) ||
            request.Currency.Trim().Length != 3 ||
            request.MonthlyPrice < 0 ||
            request.YearlyPrice < 0 ||
            request.MaxUsers <= 0 ||
            request.MaxBranches <= 0 ||
            request.MaxStorageMb < 0 ||
            request.GracePeriodDays < 0)
        {
            throw Validation(nameof(request), "Plan values are invalid.");
        }
    }

    private static void EnsureTransition(SubscriptionStatus current, SubscriptionStatus target)
    {
        bool allowed = (current, target) switch
        {
            (SubscriptionStatus.Trial, SubscriptionStatus.Active or SubscriptionStatus.Complimentary or SubscriptionStatus.Suspended or SubscriptionStatus.Expired) => true,
            (SubscriptionStatus.Active, SubscriptionStatus.Suspended or SubscriptionStatus.Expired or SubscriptionStatus.Complimentary) => true,
            (SubscriptionStatus.GracePeriod, SubscriptionStatus.Active or SubscriptionStatus.Suspended or SubscriptionStatus.Expired) => true,
            (SubscriptionStatus.Expired, SubscriptionStatus.Active or SubscriptionStatus.Complimentary) => true,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Active or SubscriptionStatus.Expired) => true,
            (SubscriptionStatus.Cancelled, SubscriptionStatus.Active or SubscriptionStatus.Complimentary) => true,
            (SubscriptionStatus.Complimentary, SubscriptionStatus.Active or SubscriptionStatus.Suspended or SubscriptionStatus.Expired) => true,
            _ when current == target => true,
            _ => false,
        };
        if (!allowed)
        {
            throw new ConflictException(
                $"Subscription cannot transition from {current} to {target}.",
                "invalid_subscription_transition");
        }
    }

    private static void Apply(SubscriptionPlan plan, SaveSubscriptionPlanRequest request, string code)
    {
        plan.Name = request.Name.Trim();
        plan.Code = code;
        plan.Description = request.Description?.Trim();
        plan.MonthlyPrice = request.MonthlyPrice;
        plan.YearlyPrice = request.YearlyPrice;
        plan.Currency = request.Currency.Trim().ToUpperInvariant();
        plan.MaxUsers = request.MaxUsers;
        plan.MaxBranches = request.MaxBranches;
        plan.MaxStorageMb = request.MaxStorageMb;
        plan.Features = request.Features.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        plan.GracePeriodDays = request.GracePeriodDays;
        plan.IsActive = request.IsActive;
    }

    private static SubscriptionPlanDto Map(SubscriptionPlan plan) =>
        new(
            plan.Id,
            plan.Name,
            plan.Code,
            plan.Description,
            plan.MonthlyPrice,
            plan.YearlyPrice,
            plan.Currency,
            plan.MaxUsers,
            plan.MaxBranches,
            plan.MaxStorageMb,
            new Dictionary<string, string>(plan.Features, StringComparer.Ordinal),
            plan.GracePeriodDays,
            plan.IsActive);

    private static OrganizationSubscriptionDto Map(OrganizationSubscription subscription) =>
        new(
            subscription.Id,
            subscription.OrganizationId,
            subscription.PlanId,
            subscription.Status,
            subscription.BillingMode,
            subscription.StartsAt,
            subscription.EndsAt,
            subscription.TrialEndsAt,
            subscription.GracePeriodEndsAt,
            subscription.SuspensionReason,
            subscription.Notes);

    private static ManualPaymentDto Map(ManualPayment payment) =>
        new(
            payment.Id,
            payment.OrganizationId,
            payment.SubscriptionId,
            payment.Amount,
            payment.Currency,
            payment.PaymentMethod,
            payment.PaymentReference,
            payment.PaymentStatus,
            payment.PaidAt,
            payment.PeriodStart,
            payment.PeriodEnd,
            payment.ReceiptFileUrl,
            payment.Note);

    private static RequestValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

public sealed class SubscriptionLifecycleService(
    IUnitOfWork unitOfWork,
    IAuthenticationTenantScope tenantScope,
    IClock clock,
    IAuditService auditService) : ISubscriptionLifecycleService
{
    public async Task<int> ProcessExpirationsAsync(CancellationToken cancellationToken = default)
    {
        using IDisposable bypass = tenantScope.BeginBypass();
        DateTimeOffset now = clock.UtcNow;
        int changed = 0;
        while (true)
        {
            PagedResult<OrganizationSubscription> page =
                await unitOfWork.Repository<OrganizationSubscription>().ListAsync(
                    subscription =>
                        subscription.Status == SubscriptionStatus.Trial &&
                            subscription.TrialEndsAt.HasValue &&
                            subscription.TrialEndsAt.Value <= now ||
                        subscription.Status == SubscriptionStatus.Active &&
                            subscription.EndsAt.HasValue &&
                            subscription.EndsAt.Value <= now ||
                        subscription.Status == SubscriptionStatus.GracePeriod &&
                            subscription.GracePeriodEndsAt.HasValue &&
                            subscription.GracePeriodEndsAt.Value <= now,
                    new PageRequest(1, PageRequest.MaximumPageSize),
                    cancellationToken);
            if (page.Items.Count == 0)
            {
                break;
            }

            foreach (OrganizationSubscription subscription in page.Items)
            {
                SubscriptionStatus? target = GetTarget(subscription, now);
                if (!target.HasValue || target.Value == subscription.Status)
                {
                    continue;
                }

                SubscriptionStatus old = subscription.Status;
                subscription.Status = target.Value;
                unitOfWork.Repository<OrganizationSubscription>().Update(subscription);
                await unitOfWork.Repository<SubscriptionHistory>().AddAsync(
                    new SubscriptionHistory
                    {
                        OrganizationId = subscription.OrganizationId,
                        SubscriptionId = subscription.Id,
                        OldStatus = old,
                        NewStatus = subscription.Status,
                        OldEndsAt = subscription.EndsAt,
                        NewEndsAt = subscription.EndsAt,
                        ActionType = subscription.Status == SubscriptionStatus.Expired
                            ? SubscriptionActionType.Expired
                            : SubscriptionActionType.Extended,
                        Reason = "Automated subscription lifecycle processing.",
                    },
                    cancellationToken);
                await auditService.RecordPlatformAsync(
                    "subscription.lifecycle-updated",
                    nameof(OrganizationSubscription),
                    subscription.Id,
                    subscription.OrganizationId,
                    cancellationToken: cancellationToken);
                changed++;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }

    private static SubscriptionStatus? GetTarget(OrganizationSubscription subscription, DateTimeOffset now)
    {
        DateTimeOffset? primaryEnd = subscription.Status == SubscriptionStatus.Trial
            ? subscription.TrialEndsAt
            : subscription.EndsAt;
        if (subscription.Status is SubscriptionStatus.Trial or SubscriptionStatus.Active &&
            primaryEnd.HasValue &&
            primaryEnd.Value <= now)
        {
            return subscription.GracePeriodEndsAt.HasValue && subscription.GracePeriodEndsAt.Value > now
                ? SubscriptionStatus.GracePeriod
                : SubscriptionStatus.Expired;
        }

        if (subscription.Status == SubscriptionStatus.GracePeriod &&
            subscription.GracePeriodEndsAt.HasValue &&
            subscription.GracePeriodEndsAt.Value <= now)
        {
            return SubscriptionStatus.Expired;
        }

        return null;
    }
}
