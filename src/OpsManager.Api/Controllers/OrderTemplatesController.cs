using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Common;
using OpsManager.Service.Orders;
using OpsManager.Service.Orders.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/order-templates")]
[Authorize(Policy = PolicyNames.OrganizationMember)]
public sealed class OrderTemplatesController(IOrderTemplateService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<OrderTemplateDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.ListAsync(new PageQuery(page, pageSize), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<OrderTemplateDto> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = PolicyNames.Manager)]
    public async Task<ActionResult<OrderTemplateDto>> Create(
        SaveOrderTemplateRequest request,
        CancellationToken cancellationToken)
    {
        OrderTemplateDto result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task<OrderTemplateDto> Update(
        Guid id,
        SaveOrderTemplateRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task Delete(Guid id, CancellationToken cancellationToken) =>
        service.DeleteAsync(id, cancellationToken);

    [HttpPost("{id:guid}/clone")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task<OrderTemplateDto> Clone(Guid id, CancellationToken cancellationToken) =>
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
    public Task<OrderTemplateItemDto> AddItem(
        Guid id,
        OrderTemplateItemRequest request,
        CancellationToken cancellationToken) =>
        service.AddItemAsync(id, request, cancellationToken);

    [HttpPatch("{id:guid}/items/{itemId:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task<OrderTemplateItemDto> UpdateItem(
        Guid id,
        Guid itemId,
        OrderTemplateItemRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateItemAsync(id, itemId, request, cancellationToken);

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task DeleteItem(Guid id, Guid itemId, CancellationToken cancellationToken) =>
        service.DeleteItemAsync(id, itemId, cancellationToken);

    [HttpPost("{id:guid}/items/reorder")]
    [Authorize(Policy = PolicyNames.Manager)]
    public Task Reorder(
        Guid id,
        ReorderOrderItemsRequest request,
        CancellationToken cancellationToken) =>
        service.ReorderAsync(id, request, cancellationToken);
}
