using OpsManager.Domain.Enums;
using OpsManager.Service.Common;

namespace OpsManager.Service.Orders.DTOs;

public sealed record OrderTemplateItemRequest(
    string Name,
    string? Description,
    UnitCode UnitCode,
    string? CustomUnitLabel,
    decimal? DefaultQuantity,
    decimal? MinimumQuantity,
    int SortOrder,
    string? ImageUrl,
    bool IsActive = true);

public sealed record OrderTemplateItemDto(
    Guid Id,
    string Name,
    string? Description,
    UnitCode UnitCode,
    string? CustomUnitLabel,
    decimal? DefaultQuantity,
    decimal? MinimumQuantity,
    int SortOrder,
    string? ImageUrl,
    bool IsActive);

public sealed record SaveOrderTemplateRequest(
    Guid BranchId,
    string Name,
    string? Description,
    Guid SourceDepartmentId,
    Guid TargetDepartmentId,
    bool RequiresApproval,
    bool AllowCustomItems,
    bool IsActive,
    IReadOnlyList<OrderTemplateItemRequest> Items);

public sealed record OrderTemplateDto(
    Guid Id,
    Guid BranchId,
    string Name,
    string? Description,
    Guid SourceDepartmentId,
    Guid TargetDepartmentId,
    bool RequiresApproval,
    bool AllowCustomItems,
    bool IsActive,
    IReadOnlyList<OrderTemplateItemDto> Items);

public sealed record ReorderOrderItemsRequest(IReadOnlyList<Guid> ItemIds);

public sealed record CreateOrderItemRequest(
    Guid? TemplateItemId,
    string? CustomName,
    string? Description,
    UnitCode? UnitCode,
    string? CustomUnitLabel,
    decimal RequestedQuantity,
    string? Note);

public sealed record CreateDepartmentOrderRequest(
    Guid? OrderTemplateId,
    Guid BranchId,
    Guid SourceDepartmentId,
    Guid TargetDepartmentId,
    DateTimeOffset? RequiredAt,
    TaskPriority Priority,
    string? GeneralNote,
    IReadOnlyList<CreateOrderItemRequest> Items);

public sealed record CreateOrderFromTemplateRequest(
    DateTimeOffset? RequiredAt,
    TaskPriority Priority,
    string? GeneralNote,
    IReadOnlyList<CreateOrderItemRequest> Items);

public sealed record DepartmentOrderItemDto(
    Guid Id,
    string Name,
    string? Description,
    UnitCode UnitCode,
    string? CustomUnitLabel,
    decimal RequestedQuantity,
    decimal FulfilledQuantity,
    decimal ReceivedQuantity,
    DepartmentOrderItemStatus Status,
    string? ItemNote,
    string? FulfillmentNote,
    bool IsCustomItem);

public sealed record DepartmentOrderDto(
    Guid Id,
    string OrderNumber,
    Guid? OrderTemplateId,
    Guid BranchId,
    Guid SourceDepartmentId,
    Guid TargetDepartmentId,
    Guid CreatedBy,
    Guid? AssignedTo,
    TaskPriority Priority,
    DepartmentOrderStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? RequiredAt,
    bool IsLate,
    string? GeneralNote,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? ReceivedAt,
    string? RejectionReason,
    IReadOnlyList<DepartmentOrderItemDto> Items);

public sealed record DepartmentOrderQuery(
    int Page = 1,
    int PageSize = 20,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    DepartmentOrderStatus? Status = null,
    Guid? SourceDepartmentId = null,
    Guid? TargetDepartmentId = null,
    Guid? AssigneeUserId = null,
    TaskPriority? Priority = null,
    bool? Late = null)
{
    public PageQuery PageQuery => new(Page, PageSize);
}

public sealed record AssignOrderRequest(Guid AssigneeUserId);
public sealed record RejectOrderRequest(string Reason);
public sealed record UpdateOrderItemRequest(
    decimal FulfilledQuantity,
    decimal ReceivedQuantity,
    DepartmentOrderItemStatus Status,
    string? FulfillmentNote);
