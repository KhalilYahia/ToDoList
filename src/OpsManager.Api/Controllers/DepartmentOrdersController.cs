using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Orders;
using OpsManager.Service.Orders.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/department-orders")]
[Authorize(Policy = PolicyNames.OrganizationMember)]
public sealed class DepartmentOrdersController(IDepartmentOrderService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<DepartmentOrderDto>> List(
        [FromQuery] DepartmentOrderQuery query,
        CancellationToken cancellationToken) =>
        service.ListAsync(query, OrderListScope.All, cancellationToken);

    [HttpGet("incoming")]
    public Task<PagedResponse<DepartmentOrderDto>> Incoming(
        [FromQuery] DepartmentOrderQuery query,
        CancellationToken cancellationToken) =>
        service.ListAsync(query, OrderListScope.Incoming, cancellationToken);

    [HttpGet("outgoing")]
    public Task<PagedResponse<DepartmentOrderDto>> Outgoing(
        [FromQuery] DepartmentOrderQuery query,
        CancellationToken cancellationToken) =>
        service.ListAsync(query, OrderListScope.Outgoing, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<DepartmentOrderDto> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<DepartmentOrderDto>> Create(
        CreateDepartmentOrderRequest request,
        CancellationToken cancellationToken)
    {
        DepartmentOrderDto result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/view")]
    public Task<DepartmentOrderDto> View(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost("{id:guid}/accept")]
    public Task<DepartmentOrderDto> Accept(Guid id, CancellationToken cancellationToken) =>
        service.ActAsync(id, OrderAction.Accept, null, cancellationToken);

    [HttpPost("{id:guid}/assign")]
    public Task<DepartmentOrderDto> Assign(
        Guid id,
        AssignOrderRequest request,
        CancellationToken cancellationToken) =>
        service.AssignAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/start")]
    public Task<DepartmentOrderDto> Start(Guid id, CancellationToken cancellationToken) =>
        service.ActAsync(id, OrderAction.Start, null, cancellationToken);

    [HttpPatch("{id:guid}/items/{itemId:guid}")]
    public Task<DepartmentOrderItemDto> UpdateItem(
        Guid id,
        Guid itemId,
        UpdateOrderItemRequest request,
        CancellationToken cancellationToken) =>
        service.UpdateItemAsync(id, itemId, request, cancellationToken);

    [HttpPost("{id:guid}/mark-ready")]
    public Task<DepartmentOrderDto> MarkReady(Guid id, CancellationToken cancellationToken) =>
        service.ActAsync(id, OrderAction.MarkReady, null, cancellationToken);

    [HttpPost("{id:guid}/deliver")]
    public Task<DepartmentOrderDto> Deliver(Guid id, CancellationToken cancellationToken) =>
        service.ActAsync(id, OrderAction.Deliver, null, cancellationToken);

    [HttpPost("{id:guid}/confirm-receipt")]
    public Task<DepartmentOrderDto> ConfirmReceipt(Guid id, CancellationToken cancellationToken) =>
        service.ActAsync(id, OrderAction.ConfirmReceipt, null, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    public Task<DepartmentOrderDto> Reject(
        Guid id,
        RejectOrderRequest request,
        CancellationToken cancellationToken) =>
        service.ActAsync(id, OrderAction.Reject, request.Reason, cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    public Task<DepartmentOrderDto> Cancel(Guid id, CancellationToken cancellationToken) =>
        service.ActAsync(id, OrderAction.Cancel, null, cancellationToken);

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
