using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Tasks;
using OpsManager.Service.Tasks.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/tasks")]
[Authorize(Policy = PolicyNames.OrganizationMember)]
public sealed class TasksController(ITaskService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<TaskDto>> List(
        [FromQuery] TaskQuery query,
        CancellationToken cancellationToken) =>
        service.ListAsync(query, false, cancellationToken);

    [HttpGet("my")]
    public Task<PagedResponse<TaskDto>> My(
        [FromQuery] TaskQuery query,
        CancellationToken cancellationToken) =>
        service.ListAsync(query, true, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<TaskDto> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = PolicyNames.SupervisorOrManager)]
    public async Task<ActionResult<TaskDistributionResponse>> Create(
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        TaskDistributionResponse result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Tasks[0].TaskId }, result);
    }

    [HttpPatch("{id:guid}")]
    public Task<TaskDto> Update(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = PolicyNames.SupervisorOrManager)]
    public Task<TaskDto> Assign(Guid id, AssignTaskRequest request, CancellationToken cancellationToken) =>
        service.AssignAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/start")]
    public Task<TaskDto> Start(Guid id, CancellationToken cancellationToken) =>
        service.StartAsync(id, cancellationToken);

    [HttpPost("{id:guid}/block")]
    public Task<TaskDto> Block(Guid id, ReasonRequest request, CancellationToken cancellationToken) =>
        service.BlockAsync(id, request.Reason, cancellationToken);

    [HttpPost("{id:guid}/resume")]
    public Task<TaskDto> Resume(Guid id, CancellationToken cancellationToken) =>
        service.ResumeAsync(id, cancellationToken);

    [HttpPost("{id:guid}/complete")]
    public Task<TaskDto> Complete(Guid id, CancellationToken cancellationToken) =>
        service.CompleteAsync(id, cancellationToken);

    [HttpPost("{id:guid}/submit-for-approval")]
    public Task<TaskDto> SubmitForApproval(Guid id, CancellationToken cancellationToken) =>
        service.SubmitForApprovalAsync(id, cancellationToken);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = PolicyNames.SupervisorOrManager)]
    public Task<TaskDto> Approve(Guid id, CancellationToken cancellationToken) =>
        service.ApproveAsync(id, cancellationToken);

    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = PolicyNames.SupervisorOrManager)]
    public Task<TaskDto> Return(Guid id, ReasonRequest request, CancellationToken cancellationToken) =>
        service.ReturnForCorrectionAsync(id, request.Reason, cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    public Task<TaskDto> Cancel(Guid id, ReasonRequest request, CancellationToken cancellationToken) =>
        service.CancelAsync(id, request.Reason, cancellationToken);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.SupervisorOrManager)]
    public Task Delete(Guid id, CancellationToken cancellationToken) =>
        service.DeleteAsync(id, cancellationToken);

    [HttpPost("{id:guid}/clone")]
    [Authorize(Policy = PolicyNames.SupervisorOrManager)]
    public Task<TaskDistributionResponse> Clone(
        Guid id,
        CloneTaskRequest request,
        CancellationToken cancellationToken) =>
        service.CloneAsync(id, request, cancellationToken);

    [HttpPatch("{taskId:guid}/items/{itemId:guid}")]
    public Task<TaskItemDto> UpdateItem(
        Guid taskId,
        Guid itemId,
        UpdateTaskItemRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateItemAsync(taskId, itemId, request, cancellationToken);

    [HttpPost("{taskId:guid}/items/{itemId:guid}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<StoredFile> AddAttachment(
        Guid taskId,
        Guid itemId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using Stream stream = file.OpenReadStream();
        return await service.AddAttachmentAsync(taskId, itemId, stream, file.FileName, file.ContentType, cancellationToken);
    }

    [HttpDelete("{taskId:guid}/items/{itemId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(
        Guid taskId,
        Guid itemId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        await service.DeleteAttachmentAsync(taskId, itemId, attachmentId, cancellationToken);
        return NoContent();
    }
}
