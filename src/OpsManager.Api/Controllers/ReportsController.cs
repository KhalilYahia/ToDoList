using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Common;
using OpsManager.Service.Reports;
using OpsManager.Service.Reports.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = PolicyNames.Manager)]
public sealed class ReportsController(IReportService service) : ControllerBase
{
    [HttpGet("tasks/summary")]
    public Task<TaskSummaryReportDto> TaskSummary(
        [FromQuery] ReportQuery query,
        CancellationToken cancellationToken) =>
        service.GetTaskSummaryAsync(query, cancellationToken);

    [HttpGet("tasks/by-department")]
    public Task<PagedResponse<ReportBreakdownDto>> TasksByDepartment(
        [FromQuery] ReportQuery query,
        CancellationToken cancellationToken) =>
        service.GetTasksByDepartmentAsync(query, cancellationToken);

    [HttpGet("tasks/by-assignee")]
    public Task<PagedResponse<ReportBreakdownDto>> TasksByAssignee(
        [FromQuery] ReportQuery query,
        CancellationToken cancellationToken) =>
        service.GetTasksByAssigneeAsync(query, cancellationToken);

    [HttpGet("tasks/overdue")]
    public Task<PagedResponse<TaskReportRowDto>> OverdueTasks(
        [FromQuery] ReportQuery query,
        CancellationToken cancellationToken) =>
        service.GetOverdueTasksAsync(query, cancellationToken);

    [HttpGet("department-orders/summary")]
    public Task<OrderSummaryReportDto> OrderSummary(
        [FromQuery] ReportQuery query,
        CancellationToken cancellationToken) =>
        service.GetOrderSummaryAsync(query, cancellationToken);

    [HttpGet("department-orders/by-route")]
    public Task<PagedResponse<OrderRouteReportDto>> OrdersByRoute(
        [FromQuery] ReportQuery query,
        CancellationToken cancellationToken) =>
        service.GetOrdersByRouteAsync(query, cancellationToken);

    [HttpGet("department-orders/top-items")]
    public Task<PagedResponse<TopOrderItemReportDto>> TopOrderItems(
        [FromQuery] ReportQuery query,
        CancellationToken cancellationToken) =>
        service.GetTopOrderItemsAsync(query, cancellationToken);

    [HttpGet("department-orders/late")]
    public Task<PagedResponse<OrderReportRowDto>> LateOrders(
        [FromQuery] ReportQuery query,
        CancellationToken cancellationToken) =>
        service.GetLateOrdersAsync(query, cancellationToken);

    [HttpGet("complaints/summary")]
    public Task<ComplaintSummaryReportDto> ComplaintSummary(
        [FromQuery] ReportQuery query,
        CancellationToken cancellationToken) =>
        service.GetComplaintSummaryAsync(query, cancellationToken);
}
