using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Domain.Enums;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Complaints;
using OpsManager.Service.Complaints.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/complaints")]
[Authorize(Policy = PolicyNames.OrganizationMember)]
public sealed class ComplaintsController(IComplaintService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<ComplaintDto>> List(
        [FromQuery] ComplaintQuery query,
        CancellationToken cancellationToken) =>
        service.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<ComplaintDto> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ComplaintDto>> Create(
        CreateComplaintRequest request,
        CancellationToken cancellationToken)
    {
        ComplaintDto result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPatch("{id:guid}")]
    public Task<ComplaintDto> Update(
        Guid id,
        UpdateComplaintRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = PolicyNames.SupervisorOrManager)]
    public Task<ComplaintDto> Assign(
        Guid id,
        AssignComplaintRequest request,
        CancellationToken cancellationToken) =>
        service.AssignAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/start-review")]
    [Authorize(Policy = PolicyNames.SupervisorOrManager)]
    public Task<ComplaintDto> StartReview(Guid id, CancellationToken cancellationToken) =>
        service.ChangeStatusAsync(id, ComplaintStatus.UnderReview, cancellationToken: cancellationToken);

    [HttpPost("{id:guid}/request-information")]
    [Authorize(Policy = PolicyNames.SupervisorOrManager)]
    public Task<ComplaintDto> RequestInformation(
        Guid id,
        ComplaintMessageRequest request,
        CancellationToken cancellationToken) =>
        service.ChangeStatusAsync(id, ComplaintStatus.InProgress, request.Message, cancellationToken);

    [HttpPost("{id:guid}/respond")]
    [Authorize(Policy = PolicyNames.SupervisorOrManager)]
    public Task<ComplaintDto> Respond(
        Guid id,
        ComplaintMessageRequest request,
        CancellationToken cancellationToken) =>
        service.ChangeStatusAsync(id, ComplaintStatus.Resolved, request.Message, cancellationToken);

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = PolicyNames.SupervisorOrManager)]
    public Task<ComplaintDto> Close(Guid id, CancellationToken cancellationToken) =>
        service.ChangeStatusAsync(id, ComplaintStatus.Closed, cancellationToken: cancellationToken);

    [HttpPost("{id:guid}/messages")]
    public Task<ComplaintMessageDto> AddMessage(
        Guid id,
        ComplaintMessageRequest request,
        CancellationToken cancellationToken) =>
        service.AddMessageAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<StoredFile> AddAttachment(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using Stream stream = file.OpenReadStream();
        return await service.AddAttachmentAsync(id, stream, file.FileName, file.ContentType, cancellationToken);
    }
}
