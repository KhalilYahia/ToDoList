using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Organizations;
using OpsManager.Service.Organizations.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/members")]
[Authorize(Policy = PolicyNames.SupervisorOrManager)]
public sealed class MembersController(IOrganizationService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<MemberDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.ListMembersAsync(new PageQuery(page, pageSize), cancellationToken);

    [HttpPost]
    [Authorize(Policy = PolicyNames.Manager)]
    public async Task<ActionResult<MemberDto>> Create(
        CreateMemberRequest request,
        CancellationToken cancellationToken)
    {
        MemberDto result = await service.CreateMemberAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.MembershipId }, result);
    }

    [HttpGet("{id:guid}")]
    public Task<MemberDto> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetMemberAsync(id, cancellationToken);

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task<MemberDto> Update(Guid id, UpdateMemberRequest request, CancellationToken cancellationToken) =>
        service.UpdateMemberAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = PolicyNames.Manager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await service.ActivateMemberAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = PolicyNames.Manager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken)
    {
        await service.SuspendMemberAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/departments")]
    [Authorize(Policy = PolicyNames.Manager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetDepartments(
        Guid id,
        SetMemberDepartmentsRequest request,
        CancellationToken cancellationToken)
    {
        await service.SetMemberDepartmentsAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = PolicyNames.Manager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        ResetMemberPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await service.ResetMemberPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/avatar")]
    [Authorize(Policy = PolicyNames.Manager)]
    public async Task<ActionResult<StoredFile>> UploadAvatar(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty avatar file is required.");
        }

        await using Stream stream = file.OpenReadStream();
        StoredFile result = await service.UploadMemberAvatarAsync(
            id,
            stream,
            file.FileName,
            file.ContentType,
            cancellationToken);
        return Ok(result);
    }
}
