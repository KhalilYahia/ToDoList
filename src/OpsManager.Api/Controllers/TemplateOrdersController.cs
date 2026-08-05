using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Orders;
using OpsManager.Service.Orders.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/order-templates/{templateId:guid}/create-order")]
[Authorize(Policy = PolicyNames.OrganizationMember)]
public sealed class TemplateOrdersController(IDepartmentOrderService service) : ControllerBase
{
    [HttpPost]
    public Task<DepartmentOrderDto> Create(
        Guid templateId,
        CreateOrderFromTemplateRequest request,
        CancellationToken cancellationToken) =>
        service.CreateFromTemplateAsync(templateId, request, cancellationToken);
}
