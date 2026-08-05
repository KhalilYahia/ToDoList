using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Common;
using OpsManager.Service.Platform;
using OpsManager.Service.Platform.DTOs;

namespace OpsManager.Api.Controllers.Platform;

[ApiController]
[Route("api/v1/platform/manual-payments")]
[Authorize(Policy = PolicyNames.PlatformUser)]
public sealed class ManualPaymentsController(IPlatformAdministrationService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<ManualPaymentDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.ListPaymentsAsync(new PageQuery(page, pageSize), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<ManualPaymentDto> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetPaymentAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task<ManualPaymentDto> Create(
        RecordManualPaymentRequest request,
        CancellationToken cancellationToken) =>
        service.RecordPaymentAsync(request, cancellationToken);

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task<ManualPaymentDto> Confirm(Guid id, CancellationToken cancellationToken) =>
        service.ChangePaymentStatusAsync(id, PaymentOperation.Confirm, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task<ManualPaymentDto> Reject(Guid id, CancellationToken cancellationToken) =>
        service.ChangePaymentStatusAsync(id, PaymentOperation.Reject, cancellationToken);

    [HttpPost("{id:guid}/refund")]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task<ManualPaymentDto> Refund(Guid id, CancellationToken cancellationToken) =>
        service.ChangePaymentStatusAsync(id, PaymentOperation.Refund, cancellationToken);
}
