using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Reports;
using OpsManager.Service.Reports.DTOs;

namespace OpsManager.Api.Controllers.Platform;

[ApiController]
[Route("api/v1/platform/reports")]
[Authorize(Policy = PolicyNames.PlatformUser)]
public sealed class PlatformReportsController(IReportService service) : ControllerBase
{
    [HttpGet("subscriptions/summary")]
    public Task<SubscriptionSummaryReportDto> SubscriptionSummary(CancellationToken cancellationToken) =>
        service.GetSubscriptionSummaryAsync(cancellationToken);

    [HttpGet("payments/summary")]
    public Task<PaymentSummaryReportDto> PaymentSummary(
        [FromQuery] ReportQuery query,
        CancellationToken cancellationToken) =>
        service.GetPaymentSummaryAsync(query, cancellationToken);
}
