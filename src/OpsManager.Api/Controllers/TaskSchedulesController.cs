using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Common;
using OpsManager.Service.Tasks;
using OpsManager.Service.Tasks.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/task-schedules")]
[Authorize(Policy = PolicyNames.SupervisorOrManager)]
public sealed class TaskSchedulesController(
    ITaskScheduleService service,
    ITaskOccurrenceGeneratorService generator) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<TaskScheduleDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.ListAsync(new PageQuery(page, pageSize), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<TaskScheduleDto> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = PolicyNames.Manager)]
    public async Task<ActionResult<TaskScheduleDto>> Create(
        SaveTaskScheduleRequest request,
        CancellationToken cancellationToken)
    {
        TaskScheduleDto result = await service.CreateAsync(request, cancellationToken);
        await generator.GenerateAsync(result.Id, null, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    public async Task<TaskScheduleDto> Update(
        Guid id,
        SaveTaskScheduleRequest request,
        CancellationToken cancellationToken)
    {
        TaskScheduleDto result = await service.UpdateAsync(id, request, cancellationToken);
        await generator.GenerateAsync(result.Id, null, cancellationToken);
        return result;
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task Delete(Guid id, CancellationToken cancellationToken) =>
        service.DeleteAsync(id, cancellationToken);

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = PolicyNames.Manager)]
    public async Task Activate(Guid id, CancellationToken cancellationToken)
    {
        await service.SetActiveAsync(id, true, cancellationToken);
        await generator.GenerateAsync(id, null, cancellationToken);
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task Deactivate(Guid id, CancellationToken cancellationToken) =>
        service.SetActiveAsync(id, false, cancellationToken);

    [HttpPost("{id:guid}/generate")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task<OccurrenceGenerationResult> Generate(
        Guid id,
        GenerateOccurrencesRequest request,
        CancellationToken cancellationToken) =>
        generator.GenerateAsync(id, request.ThroughDate, cancellationToken);
}
