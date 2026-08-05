using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Organizations;
using OpsManager.Service.Organizations.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/organization")]
[Authorize(Policy = PolicyNames.OrganizationMember)]
public sealed class OrganizationController(IOrganizationService service) : ControllerBase
{
    [HttpGet]
    public Task<OrganizationDto> Get(CancellationToken cancellationToken) =>
        service.GetOrganizationAsync(cancellationToken);

    [HttpPatch]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task<OrganizationDto> Update(UpdateOrganizationRequest request, CancellationToken cancellationToken) =>
        service.UpdateOrganizationAsync(request, cancellationToken);
}
