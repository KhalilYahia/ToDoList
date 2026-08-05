using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Common;
using OpsManager.Service.Organizations;
using OpsManager.Service.Organizations.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/branches")]
[Authorize(Policy = PolicyNames.OrganizationMember)]
public sealed class BranchesController(IOrganizationService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<BranchDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.ListBranchesAsync(new PageQuery(page, pageSize), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<BranchDto> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetBranchAsync(id, cancellationToken);

}
