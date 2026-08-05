using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Reports.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Reports;

public interface IReportService
{
    Task<TaskSummaryReportDto> GetTaskSummaryAsync(ReportQuery query, CancellationToken cancellationToken = default);
    Task<PagedResponse<ReportBreakdownDto>> GetTasksByDepartmentAsync(ReportQuery query, CancellationToken cancellationToken = default);
    Task<PagedResponse<ReportBreakdownDto>> GetTasksByAssigneeAsync(ReportQuery query, CancellationToken cancellationToken = default);
    Task<PagedResponse<TaskReportRowDto>> GetOverdueTasksAsync(ReportQuery query, CancellationToken cancellationToken = default);
    Task<OrderSummaryReportDto> GetOrderSummaryAsync(ReportQuery query, CancellationToken cancellationToken = default);
    Task<PagedResponse<OrderRouteReportDto>> GetOrdersByRouteAsync(ReportQuery query, CancellationToken cancellationToken = default);
    Task<PagedResponse<TopOrderItemReportDto>> GetTopOrderItemsAsync(ReportQuery query, CancellationToken cancellationToken = default);
    Task<PagedResponse<OrderReportRowDto>> GetLateOrdersAsync(ReportQuery query, CancellationToken cancellationToken = default);
    Task<ComplaintSummaryReportDto> GetComplaintSummaryAsync(ReportQuery query, CancellationToken cancellationToken = default);
    Task<SubscriptionSummaryReportDto> GetSubscriptionSummaryAsync(CancellationToken cancellationToken = default);
    Task<PaymentSummaryReportDto> GetPaymentSummaryAsync(ReportQuery query, CancellationToken cancellationToken = default);
}

public sealed class ReportService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    ISubscriptionAccessService subscriptionAccess,
    IClock clock) : IReportService
{
    public async Task<TaskSummaryReportDto> GetTaskSummaryAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = await RequireTenantReportAccessAsync(query, cancellationToken);
        DateTimeOffset now = clock.UtcNow;
        int total = await CountTasksAsync(organizationId, query, null, false, cancellationToken);
        int completed = await CountTasksAsync(
            organizationId,
            query,
            [OperationalTaskStatus.Completed],
            false,
            cancellationToken);
        int inProgress = await CountTasksAsync(
            organizationId,
            query,
            [OperationalTaskStatus.InProgress, OperationalTaskStatus.Blocked, OperationalTaskStatus.PendingApproval],
            false,
            cancellationToken);
        int cancelled = await CountTasksAsync(organizationId, query, [OperationalTaskStatus.Cancelled], false, cancellationToken);
        int overdue = await CountTasksAsync(organizationId, query, null, true, cancellationToken);
        int distributionCount = await CountTaskDistributionsAsync(organizationId, query, cancellationToken);
        IReadOnlyList<TaskTiming> timings = await unitOfWork.Repository<OperationalTask>().ProjectAsync(
            task => task.OrganizationId == organizationId &&
                task.ScheduledStartAt >= query.From &&
                task.ScheduledStartAt < query.To &&
                (!query.BranchId.HasValue || task.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue || task.DepartmentId == query.DepartmentId.Value) &&
                (!query.AssigneeUserId.HasValue || task.AssigneeUserId == query.AssigneeUserId.Value) &&
                task.CompletedAt.HasValue,
            task => new TaskTiming(task.StartedAt, task.CompletedAt, task.DueAt),
            cancellationToken);
        int onTime = timings.Count(item => item.CompletedAt <= item.DueAt);
        double? average = AverageMinutes(
            timings.Where(item => item.StartedAt.HasValue && item.CompletedAt.HasValue)
                .Select(item => (item.CompletedAt!.Value - item.StartedAt!.Value).TotalMinutes));
        return new TaskSummaryReportDto(
            total,
            completed,
            inProgress,
            overdue,
            cancelled,
            Rate(completed, total),
            Rate(onTime, timings.Count),
            average,
            distributionCount,
            total,
            total - completed - cancelled);
    }

    public async Task<PagedResponse<ReportBreakdownDto>> GetTasksByDepartmentAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = await RequireTenantReportAccessAsync(query, cancellationToken);
        PagedResult<Department> departments = await unitOfWork.Repository<Department>().ListAsync(
            department => department.OrganizationId == organizationId &&
                (!query.BranchId.HasValue || department.BranchId == query.BranchId.Value),
            query.PageQuery.ToDomain(),
            cancellationToken);
        List<ReportBreakdownDto> rows = [];
        foreach (Department department in departments.Items)
        {
            ReportQuery scoped = query with { DepartmentId = department.Id };
            int total = await CountTasksAsync(organizationId, scoped, null, false, cancellationToken);
            int completed = await CountTasksAsync(
                organizationId,
                scoped,
                [OperationalTaskStatus.Completed],
                false,
                cancellationToken);
            int overdue = await CountTasksAsync(organizationId, scoped, null, true, cancellationToken);
            rows.Add(new ReportBreakdownDto(department.Id, department.Name, total, completed, overdue));
        }

        return new PagedResponse<ReportBreakdownDto>(
            rows,
            departments.Page,
            departments.PageSize,
            departments.TotalCount);
    }

    public async Task<PagedResponse<ReportBreakdownDto>> GetTasksByAssigneeAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = await RequireTenantReportAccessAsync(query, cancellationToken);
        PagedResult<OrganizationMember> members = await unitOfWork.Repository<OrganizationMember>().ListAsync(
            member => member.OrganizationId == organizationId && member.IsActive,
            query.PageQuery.ToDomain(),
            cancellationToken);
        List<ReportBreakdownDto> rows = [];
        foreach (OrganizationMember member in members.Items)
        {
            User? user = await unitOfWork.Repository<User>().GetByIdAsync(member.UserId, cancellationToken);
            ReportQuery scoped = query with { AssigneeUserId = member.UserId };
            int total = await CountTasksAsync(organizationId, scoped, null, false, cancellationToken);
            int completed = await CountTasksAsync(
                organizationId,
                scoped,
                [OperationalTaskStatus.Completed],
                false,
                cancellationToken);
            int overdue = await CountTasksAsync(organizationId, scoped, null, true, cancellationToken);
            rows.Add(new ReportBreakdownDto(member.UserId, user?.FullName ?? "Unknown user", total, completed, overdue));
        }

        return new PagedResponse<ReportBreakdownDto>(rows, members.Page, members.PageSize, members.TotalCount);
    }

    public async Task<PagedResponse<TaskReportRowDto>> GetOverdueTasksAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = await RequireTenantReportAccessAsync(query, cancellationToken);
        DateTimeOffset now = clock.UtcNow;
        PagedResult<OperationalTask> result = await unitOfWork.Repository<OperationalTask>().ListAsync(
            task => task.OrganizationId == organizationId &&
                task.ScheduledStartAt >= query.From &&
                task.ScheduledStartAt < query.To &&
                task.DueAt < now &&
                task.Status != OperationalTaskStatus.Completed &&
                task.Status != OperationalTaskStatus.Cancelled &&
                (!query.BranchId.HasValue || task.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue || task.DepartmentId == query.DepartmentId.Value) &&
                (!query.AssigneeUserId.HasValue || task.AssigneeUserId == query.AssigneeUserId.Value),
            query.PageQuery.ToDomain(),
            cancellationToken);
        return PagedResponse.Map(
            result,
            task => new TaskReportRowDto(
                task.Id,
                task.Title,
                task.DepartmentId,
                task.AssigneeUserId,
                task.Status,
                task.DueAt,
                true));
    }

    public async Task<OrderSummaryReportDto> GetOrderSummaryAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = await RequireTenantReportAccessAsync(query, cancellationToken);
        DateTimeOffset now = clock.UtcNow;
        int total = await CountOrdersAsync(organizationId, query, null, false, cancellationToken);
        int received = await CountOrdersAsync(
            organizationId,
            query,
            [DepartmentOrderStatus.Received],
            false,
            cancellationToken);
        int rejected = await CountOrdersAsync(
            organizationId,
            query,
            [DepartmentOrderStatus.Rejected, DepartmentOrderStatus.Cancelled],
            false,
            cancellationToken);
        int late = await CountOrdersAsync(organizationId, query, null, true, cancellationToken);
        IReadOnlyList<OrderTiming> timings = await unitOfWork.Repository<DepartmentOrder>().ProjectAsync(
            order => order.OrganizationId == organizationId &&
                order.RequestedAt >= query.From &&
                order.RequestedAt < query.To &&
                (!query.BranchId.HasValue || order.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue ||
                    order.SourceDepartmentId == query.DepartmentId.Value ||
                    order.TargetDepartmentId == query.DepartmentId.Value),
            order => new OrderTiming(
                order.RequestedAt,
                order.AcceptedAt,
                order.ReadyAt,
                order.DeliveredAt,
                order.ReceivedAt),
            cancellationToken);
        return new OrderSummaryReportDto(
            total,
            received,
            rejected,
            late,
            AverageMinutes(timings.Where(item => item.AcceptedAt.HasValue)
                .Select(item => (item.AcceptedAt!.Value - item.RequestedAt).TotalMinutes)),
            AverageMinutes(timings.Where(item => item.AcceptedAt.HasValue && item.ReadyAt.HasValue)
                .Select(item => (item.ReadyAt!.Value - item.AcceptedAt!.Value).TotalMinutes)),
            AverageMinutes(timings.Where(item => item.DeliveredAt.HasValue && item.ReceivedAt.HasValue)
                .Select(item => (item.ReceivedAt!.Value - item.DeliveredAt!.Value).TotalMinutes)));
    }

    public async Task<PagedResponse<OrderRouteReportDto>> GetOrdersByRouteAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = await RequireTenantReportAccessAsync(query, cancellationToken);
        DateTimeOffset now = clock.UtcNow;
        IReadOnlyList<OrderRouteProjection> values = await unitOfWork.Repository<DepartmentOrder>().ProjectAsync(
            order => order.OrganizationId == organizationId &&
                order.RequestedAt >= query.From &&
                order.RequestedAt < query.To &&
                (!query.BranchId.HasValue || order.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue ||
                    order.SourceDepartmentId == query.DepartmentId.Value ||
                    order.TargetDepartmentId == query.DepartmentId.Value),
            order => new OrderRouteProjection(
                order.SourceDepartmentId,
                order.TargetDepartmentId,
                order.Status,
                order.RequiredAt),
            cancellationToken);
        List<OrderRouteReportDto> rows = values
            .GroupBy(item => new { item.SourceDepartmentId, item.TargetDepartmentId })
            .Select(group => new OrderRouteReportDto(
                group.Key.SourceDepartmentId,
                group.Key.TargetDepartmentId,
                group.Count(),
                group.Count(item => item.Status == DepartmentOrderStatus.Received),
                group.Count(item => item.RequiredAt.HasValue &&
                    item.RequiredAt.Value < now &&
                    item.Status is not (
                        DepartmentOrderStatus.Delivered or
                        DepartmentOrderStatus.Received or
                        DepartmentOrderStatus.Rejected or
                        DepartmentOrderStatus.Cancelled))))
            .OrderByDescending(item => item.Total)
            .ToList();
        return ManualPage(rows, query.PageQuery);
    }

    public async Task<PagedResponse<TopOrderItemReportDto>> GetTopOrderItemsAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = await RequireTenantReportAccessAsync(query, cancellationToken);
        IReadOnlyList<OrderItemProjection> values =
            await unitOfWork.Repository<DepartmentOrderItem>()
                .ProjectJoinAsync<DepartmentOrder, Guid, OrderItemProjection>(
            item => item.OrganizationId == organizationId,
            order => order.OrganizationId == organizationId &&
                order.RequestedAt >= query.From &&
                order.RequestedAt < query.To &&
                (!query.BranchId.HasValue || order.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue ||
                    order.SourceDepartmentId == query.DepartmentId.Value ||
                    order.TargetDepartmentId == query.DepartmentId.Value),
            item => item.DepartmentOrderId,
            order => order.Id,
            (item, _) => new OrderItemProjection(item.ItemNameSnapshot, item.RequestedQuantity),
            cancellationToken);
        List<TopOrderItemReportDto> rows = values
            .GroupBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TopOrderItemReportDto(
                group.Key,
                group.Sum(item => item.Quantity),
                group.Count()))
            .OrderByDescending(item => item.RequestedQuantity)
            .ToList();
        return ManualPage(rows, query.PageQuery);
    }

    public async Task<PagedResponse<OrderReportRowDto>> GetLateOrdersAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = await RequireTenantReportAccessAsync(query, cancellationToken);
        DateTimeOffset now = clock.UtcNow;
        PagedResult<DepartmentOrder> result = await unitOfWork.Repository<DepartmentOrder>().ListAsync(
            order => order.OrganizationId == organizationId &&
                order.RequestedAt >= query.From &&
                order.RequestedAt < query.To &&
                order.RequiredAt.HasValue &&
                order.RequiredAt.Value < now &&
                order.Status != DepartmentOrderStatus.Delivered &&
                order.Status != DepartmentOrderStatus.Received &&
                order.Status != DepartmentOrderStatus.Rejected &&
                order.Status != DepartmentOrderStatus.Cancelled &&
                (!query.BranchId.HasValue || order.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue ||
                    order.SourceDepartmentId == query.DepartmentId.Value ||
                    order.TargetDepartmentId == query.DepartmentId.Value),
            query.PageQuery.ToDomain(),
            cancellationToken);
        return PagedResponse.Map(
            result,
            order => new OrderReportRowDto(
                order.Id,
                order.OrderNumber,
                order.SourceDepartmentId,
                order.TargetDepartmentId,
                order.Status,
                order.RequiredAt,
                true));
    }

    public async Task<ComplaintSummaryReportDto> GetComplaintSummaryAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = await RequireTenantReportAccessAsync(query, cancellationToken);
        int total = await CountComplaintsAsync(organizationId, query, null, cancellationToken);
        int closed = await CountComplaintsAsync(organizationId, query, [ComplaintStatus.Closed], cancellationToken);
        IReadOnlyList<ComplaintTiming> timings = await unitOfWork.Repository<Complaint>().ProjectAsync(
            complaint => complaint.OrganizationId == organizationId &&
                complaint.CreatedAt >= query.From &&
                complaint.CreatedAt < query.To &&
                (!query.BranchId.HasValue || complaint.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue || complaint.TargetDepartmentId == query.DepartmentId.Value),
            complaint => new ComplaintTiming(complaint.CreatedAt, complaint.ReviewedAt, complaint.ClosedAt),
            cancellationToken);
        return new ComplaintSummaryReportDto(
            total,
            total - closed,
            closed,
            AverageMinutes(timings.Where(item => item.ReviewedAt.HasValue)
                .Select(item => (item.ReviewedAt!.Value - item.CreatedAt).TotalMinutes)),
            AverageMinutes(timings.Where(item => item.ClosedAt.HasValue)
                .Select(item => (item.ClosedAt!.Value - item.CreatedAt).TotalMinutes)));
    }

    public async Task<SubscriptionSummaryReportDto> GetSubscriptionSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        RequirePlatformAccess();
        DateTimeOffset soon = clock.UtcNow.AddDays(30);
        int trial = await unitOfWork.Repository<OrganizationSubscription>()
            .CountAsync(item => item.Status == SubscriptionStatus.Trial, cancellationToken);
        int active = await unitOfWork.Repository<OrganizationSubscription>()
            .CountAsync(item => item.Status == SubscriptionStatus.Active || item.Status == SubscriptionStatus.Complimentary, cancellationToken);
        int grace = await unitOfWork.Repository<OrganizationSubscription>()
            .CountAsync(item => item.Status == SubscriptionStatus.GracePeriod, cancellationToken);
        int expired = await unitOfWork.Repository<OrganizationSubscription>()
            .CountAsync(item => item.Status == SubscriptionStatus.Expired, cancellationToken);
        int suspended = await unitOfWork.Repository<OrganizationSubscription>()
            .CountAsync(item => item.Status == SubscriptionStatus.Suspended, cancellationToken);
        int trialSoon = await unitOfWork.Repository<OrganizationSubscription>().CountAsync(
            item => item.Status == SubscriptionStatus.Trial &&
                item.TrialEndsAt.HasValue &&
                item.TrialEndsAt.Value <= soon,
            cancellationToken);
        int activeSoon = await unitOfWork.Repository<OrganizationSubscription>().CountAsync(
            item => (item.Status == SubscriptionStatus.Active || item.Status == SubscriptionStatus.Complimentary) &&
                item.EndsAt.HasValue &&
                item.EndsAt.Value <= soon,
            cancellationToken);
        return new SubscriptionSummaryReportDto(trial, active, grace, expired, suspended, trialSoon, activeSoon);
    }

    public async Task<PaymentSummaryReportDto> GetPaymentSummaryAsync(
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformAccess();
        Validate(query);
        IReadOnlyList<PaymentProjection> payments = await unitOfWork.Repository<ManualPayment>().ProjectAsync(
            payment => payment.PaymentStatus == PaymentStatus.Confirmed &&
                payment.PaidAt.HasValue &&
                payment.PaidAt.Value >= query.From &&
                payment.PaidAt.Value < query.To,
            payment => new PaymentProjection(payment.Currency, payment.Amount),
            cancellationToken);
        IReadOnlyList<PaymentCurrencySummaryDto> groups = payments
            .GroupBy(item => item.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PaymentCurrencySummaryDto(
                group.Key,
                group.Sum(item => item.Amount),
                group.Count()))
            .OrderBy(item => item.Currency, StringComparer.Ordinal)
            .ToArray();
        return new PaymentSummaryReportDto(groups);
    }

    private async Task<Guid> RequireTenantReportAccessAsync(ReportQuery query, CancellationToken cancellationToken)
    {
        Validate(query);
        Guid organizationId = AuthorizationGuard.RequireSupervisorOrManager(currentUser);
        await subscriptionAccess.EnsureReadAllowedAsync(
            organizationId,
            OpsManager.Domain.Constants.SubscriptionFeatureKeys.Reports,
            cancellationToken);
        return organizationId;
    }

    private async Task<int> CountTasksAsync(
        Guid organizationId,
        ReportQuery query,
        IReadOnlyList<OperationalTaskStatus>? statuses,
        bool overdue,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.UtcNow;
        return await unitOfWork.Repository<OperationalTask>().CountAsync(
            task => task.OrganizationId == organizationId &&
                task.ScheduledStartAt >= query.From &&
                task.ScheduledStartAt < query.To &&
                (!query.BranchId.HasValue || task.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue || task.DepartmentId == query.DepartmentId.Value) &&
                (!query.AssigneeUserId.HasValue || task.AssigneeUserId == query.AssigneeUserId.Value) &&
                (statuses == null || statuses.Contains(task.Status)) &&
                (!overdue ||
                    task.DueAt < now &&
                    task.Status != OperationalTaskStatus.Completed &&
                    task.Status != OperationalTaskStatus.Cancelled),
            cancellationToken);
    }

    private async Task<int> CountTaskDistributionsAsync(
        Guid organizationId,
        ReportQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid?> distributionIds = await unitOfWork.Repository<OperationalTask>().ProjectAsync(
            task => task.OrganizationId == organizationId &&
                task.ScheduledStartAt >= query.From &&
                task.ScheduledStartAt < query.To &&
                (!query.BranchId.HasValue || task.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue || task.DepartmentId == query.DepartmentId.Value) &&
                (!query.AssigneeUserId.HasValue || task.AssigneeUserId == query.AssigneeUserId.Value),
            task => task.TaskDistributionId,
            cancellationToken);
        return distributionIds.Where(id => id.HasValue).Distinct().Count();
    }

    private async Task<int> CountOrdersAsync(
        Guid organizationId,
        ReportQuery query,
        IReadOnlyList<DepartmentOrderStatus>? statuses,
        bool late,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.UtcNow;
        return await unitOfWork.Repository<DepartmentOrder>().CountAsync(
            order => order.OrganizationId == organizationId &&
                order.RequestedAt >= query.From &&
                order.RequestedAt < query.To &&
                (!query.BranchId.HasValue || order.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue ||
                    order.SourceDepartmentId == query.DepartmentId.Value ||
                    order.TargetDepartmentId == query.DepartmentId.Value) &&
                (statuses == null || statuses.Contains(order.Status)) &&
                (!late ||
                    order.RequiredAt.HasValue &&
                    order.RequiredAt.Value < now &&
                    order.Status != DepartmentOrderStatus.Delivered &&
                    order.Status != DepartmentOrderStatus.Received &&
                    order.Status != DepartmentOrderStatus.Rejected &&
                    order.Status != DepartmentOrderStatus.Cancelled),
            cancellationToken);
    }

    private Task<int> CountComplaintsAsync(
        Guid organizationId,
        ReportQuery query,
        IReadOnlyList<ComplaintStatus>? statuses,
        CancellationToken cancellationToken) =>
        unitOfWork.Repository<Complaint>().CountAsync(
            complaint => complaint.OrganizationId == organizationId &&
                complaint.CreatedAt >= query.From &&
                complaint.CreatedAt < query.To &&
                (!query.BranchId.HasValue || complaint.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue || complaint.TargetDepartmentId == query.DepartmentId.Value) &&
                (statuses == null || statuses.Contains(complaint.Status)),
            cancellationToken);

    private void RequirePlatformAccess()
    {
        if (!currentUser.IsAuthenticated || currentUser.PlatformUserId is null)
        {
            throw new ForbiddenAccessException("Platform access is required.");
        }
    }

    private static void Validate(ReportQuery query)
    {
        if (query.To <= query.From)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                [nameof(query.To)] = ["To must be later than From. Date ranges use [from, to) UTC boundaries."],
            });
        }
    }

    private static decimal Rate(int numerator, int denominator) =>
        denominator == 0 ? 0m : Math.Round((decimal)numerator / denominator * 100m, 2);

    private static double? AverageMinutes(IEnumerable<double> values)
    {
        double[] materialized = values.ToArray();
        return materialized.Length == 0 ? null : Math.Round(materialized.Average(), 2);
    }

    private static PagedResponse<T> ManualPage<T>(IReadOnlyList<T> values, PageQuery page)
    {
        PageRequest request = page.ToDomain();
        return new PagedResponse<T>(
            values.Skip(request.Skip).Take(request.PageSize).ToArray(),
            request.Page,
            request.PageSize,
            values.Count);
    }

    private sealed record TaskTiming(DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, DateTimeOffset DueAt);
    private sealed record OrderTiming(
        DateTimeOffset RequestedAt,
        DateTimeOffset? AcceptedAt,
        DateTimeOffset? ReadyAt,
        DateTimeOffset? DeliveredAt,
        DateTimeOffset? ReceivedAt);
    private sealed record ComplaintTiming(
        DateTimeOffset CreatedAt,
        DateTimeOffset? ReviewedAt,
        DateTimeOffset? ClosedAt);
    private sealed record OrderRouteProjection(
        Guid SourceDepartmentId,
        Guid TargetDepartmentId,
        DepartmentOrderStatus Status,
        DateTimeOffset? RequiredAt);
    private sealed record OrderItemProjection(string ItemName, decimal Quantity);
    private sealed record PaymentProjection(string Currency, decimal Amount);
}
