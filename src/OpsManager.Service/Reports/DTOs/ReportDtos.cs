using OpsManager.Domain.Enums;
using OpsManager.Service.Common;

namespace OpsManager.Service.Reports.DTOs;

public sealed record ReportQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? BranchId = null,
    Guid? DepartmentId = null,
    Guid? AssigneeUserId = null,
    int Page = 1,
    int PageSize = 20)
{
    public PageQuery PageQuery => new(Page, PageSize);
}

public sealed record TaskSummaryReportDto(
    int Total,
    int CompletedOrApproved,
    int InProgress,
    int Overdue,
    int Cancelled,
    decimal CompletionRate,
    decimal OnTimeCompletionRate,
    double? AverageCompletionMinutes,
    int DistributionCount,
    int TaskExecutionCount,
    int PendingExecutionCount);

public sealed record ReportBreakdownDto(Guid? Id, string Label, int Total, int Completed, int LateOrOverdue);

public sealed record TaskReportRowDto(
    Guid Id,
    string Title,
    Guid DepartmentId,
    Guid? AssigneeUserId,
    OperationalTaskStatus Status,
    DateTimeOffset DueAt,
    bool IsOverdue);

public sealed record OrderSummaryReportDto(
    int Total,
    int Received,
    int RejectedOrCancelled,
    int Late,
    double? AverageAcceptanceMinutes,
    double? AveragePreparationMinutes,
    double? AverageDeliveryToReceiptMinutes);

public sealed record OrderRouteReportDto(
    Guid SourceDepartmentId,
    Guid TargetDepartmentId,
    int Total,
    int Received,
    int Late);

public sealed record TopOrderItemReportDto(string ItemName, decimal RequestedQuantity, int OrderLineCount);

public sealed record OrderReportRowDto(
    Guid Id,
    string OrderNumber,
    Guid SourceDepartmentId,
    Guid TargetDepartmentId,
    DepartmentOrderStatus Status,
    DateTimeOffset? RequiredAt,
    bool IsLate);

public sealed record ComplaintSummaryReportDto(
    int Total,
    int Open,
    int Closed,
    double? AverageFirstReviewMinutes,
    double? AverageCloseMinutes);

public sealed record SubscriptionSummaryReportDto(
    int Trialing,
    int Active,
    int Grace,
    int Expired,
    int Suspended,
    int TrialsExpiringSoon,
    int SubscriptionsExpiringSoon);

public sealed record PaymentCurrencySummaryDto(string Currency, decimal ConfirmedAmount, int ConfirmedCount);
public sealed record PaymentSummaryReportDto(IReadOnlyList<PaymentCurrencySummaryDto> ByCurrency);
