using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Tasks;
using OpsManager.Service.Tasks.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/task-templates")]
[Authorize(Policy = PolicyNames.OrganizationMember)]
public sealed class TaskTemplatesController(ITaskTemplateService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<TaskTemplateDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.ListAsync(new PageQuery(page, pageSize), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<TaskTemplateDto> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = PolicyNames.Manager)]
    public async Task<ActionResult<TaskTemplateDto>> Create(
        SaveTaskTemplateRequest request,
        CancellationToken cancellationToken)
    {
        TaskTemplateDto result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task<TaskTemplateDto> Update(
        Guid id,
        SaveTaskTemplateRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/clone")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task<TaskTemplateDto> Clone(Guid id, CancellationToken cancellationToken) =>
        service.CloneAsync(id, cancellationToken);

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task Activate(Guid id, CancellationToken cancellationToken) =>
        service.SetActiveAsync(id, true, cancellationToken);

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task Deactivate(Guid id, CancellationToken cancellationToken) =>
        service.SetActiveAsync(id, false, cancellationToken);

    [HttpPost("{id:guid}/items")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task<ChecklistDefinitionDto> AddItem(
        Guid id,
        ChecklistDefinitionRequest request,
        CancellationToken cancellationToken) =>
        service.AddItemAsync(id, request, cancellationToken);

    [HttpPatch("{id:guid}/items/{itemId:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task<ChecklistDefinitionDto> UpdateItem(
        Guid id,
        Guid itemId,
        ChecklistDefinitionRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateItemAsync(id, itemId, request, cancellationToken);

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    public async Task<IActionResult> DeleteItem(Guid id, Guid itemId, CancellationToken cancellationToken)
    {
        await service.DeleteItemAsync(id, itemId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/items/reorder")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task Reorder(
        Guid id,
        ReorderItemsRequest request,
        CancellationToken cancellationToken) =>
        service.ReorderItemsAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/items/{itemId:guid}/attachments")]
    [Authorize(Policy = PolicyNames.Manager)]
    [Consumes("multipart/form-data")]
    public async Task<StoredFile> AddInstruction(
        Guid id,
        Guid itemId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using Stream stream = file.OpenReadStream();
        return await service.AddInstructionAsync(id, itemId, stream, file.FileName, file.ContentType, cancellationToken);
    }
}
