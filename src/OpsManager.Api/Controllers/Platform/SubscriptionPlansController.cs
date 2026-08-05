using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Common;
using OpsManager.Service.Platform;
using OpsManager.Service.Platform.DTOs;

namespace OpsManager.Api.Controllers.Platform;

[ApiController]
[Route("api/v1/platform/subscription-plans")]
[Authorize(Policy = PolicyNames.PlatformUser)]
public sealed class SubscriptionPlansController(IPlatformAdministrationService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<SubscriptionPlanDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.ListPlansAsync(new PageQuery(page, pageSize), cancellationToken);

    [HttpPost]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task<SubscriptionPlanDto> Create(
        SaveSubscriptionPlanRequest request,
        CancellationToken cancellationToken) =>
        service.CreatePlanAsync(request, cancellationToken);

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task<SubscriptionPlanDto> Update(
        Guid id,
        SaveSubscriptionPlanRequest request,
        CancellationToken cancellationToken) =>
        service.UpdatePlanAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task Activate(Guid id, CancellationToken cancellationToken) =>
        service.SetPlanActiveAsync(id, true, cancellationToken);

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task Deactivate(Guid id, CancellationToken cancellationToken) =>
        service.SetPlanActiveAsync(id, false, cancellationToken);
}
