using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Common;
using OpsManager.Service.Organizations;
using OpsManager.Service.Organizations.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/departments")]
[Authorize(Policy = PolicyNames.OrganizationMember)]
public sealed class DepartmentsController(IOrganizationService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<DepartmentDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? branchId = null,
        CancellationToken cancellationToken = default) =>
        service.ListDepartmentsAsync(new PageQuery(page, pageSize), branchId, cancellationToken);

    [HttpPost]
    [Authorize(Policy = PolicyNames.Manager)]
    public async Task<ActionResult<DepartmentDto>> Create(
        SaveDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        DepartmentDto result = await service.CreateDepartmentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public Task<DepartmentDto> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetDepartmentAsync(id, cancellationToken);

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task<DepartmentDto> Update(Guid id, SaveDepartmentRequest request, CancellationToken cancellationToken) =>
        service.UpdateDepartmentAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteDepartmentAsync(id, cancellationToken);
        return NoContent();
    }
}
