using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Common;
using OpsManager.Service.Platform;
using OpsManager.Service.Platform.DTOs;

namespace OpsManager.Api.Controllers.Platform;

[ApiController]
[Route("api/v1/platform/organizations")]
[Authorize(Policy = PolicyNames.PlatformUser)]
public sealed class PlatformOrganizationsController(IPlatformAdministrationService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<PlatformOrganizationDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.ListOrganizationsAsync(new PageQuery(page, pageSize), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<PlatformOrganizationDto> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetOrganizationAsync(id, cancellationToken);

    [HttpGet("{id:guid}/subscription")]
    public Task<OrganizationSubscriptionDto> Subscription(Guid id, CancellationToken cancellationToken) =>
        service.GetSubscriptionAsync(id, cancellationToken);

    [HttpPost("{id:guid}/subscription/activate")]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task<OrganizationSubscriptionDto> Activate(
        Guid id,
        ActivateSubscriptionRequest request,
        CancellationToken cancellationToken) =>
        service.ActivateAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/subscription/extend")]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task<OrganizationSubscriptionDto> Extend(
        Guid id,
        ExtendSubscriptionRequest request,
        CancellationToken cancellationToken) =>
        service.ExtendAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/subscription/change-plan")]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task<OrganizationSubscriptionDto> ChangePlan(
        Guid id,
        ChangeSubscriptionPlanRequest request,
        CancellationToken cancellationToken) =>
        service.ChangePlanAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/subscription/suspend")]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task<OrganizationSubscriptionDto> Suspend(
        Guid id,
        SuspendSubscriptionRequest request,
        CancellationToken cancellationToken) =>
        service.ChangeStatusAsync(id, SubscriptionOperation.Suspend, request.Reason, cancellationToken);

    [HttpPost("{id:guid}/subscription/reactivate")]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task<OrganizationSubscriptionDto> Reactivate(
        Guid id,
        SubscriptionReasonRequest request,
        CancellationToken cancellationToken) =>
        service.ChangeStatusAsync(id, SubscriptionOperation.Reactivate, request.Reason, cancellationToken);

    [HttpPost("{id:guid}/subscription/expire")]
    [Authorize(Policy = PolicyNames.PlatformAdministrator)]
    public Task<OrganizationSubscriptionDto> Expire(
        Guid id,
        SubscriptionReasonRequest request,
        CancellationToken cancellationToken) =>
        service.ChangeStatusAsync(id, SubscriptionOperation.Expire, request.Reason, cancellationToken);
}
