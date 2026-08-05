using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Tasks;
using OpsManager.Service.Tasks.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/task-templates/{templateId:guid}/create-task")]
[Authorize(Policy = PolicyNames.OrganizationMember)]
public sealed class TemplateTasksController(ITaskService service) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = PolicyNames.SupervisorOrManager)]
    public Task<TaskDistributionResponse> Create(
        Guid templateId,
        CreateTaskFromTemplateRequest request,
        CancellationToken cancellationToken) =>
        service.CreateFromTemplateAsync(templateId, request, cancellationToken);
}
