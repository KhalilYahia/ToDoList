using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Common;
using OpsManager.Service.Organizations.DTOs;
using OpsManager.Service.Platform;

namespace OpsManager.Api.Controllers.Platform;

[ApiController]
[Route("api/v1/platform/organizations/{organizationId:guid}/branches")]
[Authorize(Policy = PolicyNames.PlatformAdministrator)]
public sealed class PlatformBranchesController(
    IPlatformBranchService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<BranchDto>> List(
        Guid organizationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.ListAsync(
            organizationId,
            new PageQuery(page, pageSize),
            cancellationToken);

    [HttpPost]
    public async Task<ActionResult<BranchDto>> Add(
        Guid organizationId,
        SaveBranchRequest request,
        CancellationToken cancellationToken)
    {
        BranchDto branch =
            await service.AddAsync(organizationId, request, cancellationToken);
        return Created(
            $"/api/v1/platform/organizations/{organizationId}/branches",
            branch);
    }

    [HttpPatch("{branchId:guid}")]
    public Task<BranchDto> Update(
        Guid organizationId,
        Guid branchId,
        SaveBranchRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(
            organizationId,
            branchId,
            request,
            cancellationToken);

    [HttpDelete("{branchId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(
            organizationId,
            branchId,
            cancellationToken);
        return NoContent();
    }
}
