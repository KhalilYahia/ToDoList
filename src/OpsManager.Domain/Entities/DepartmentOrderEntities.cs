using OpsManager.Domain.Common;
using OpsManager.Domain.Enums;

namespace OpsManager.Domain.Entities;

public sealed class OrderTemplate : TenantSoftDeletableEntity
{
    private OrderTemplate() { }

    public OrderTemplate(
        Guid organizationId,
        Guid branchId,
        string name,
        Guid sourceDepartmentId,
        Guid targetDepartmentId,
        Guid createdBy)
    {
        if (sourceDepartmentId == targetDepartmentId)
        {
            throw new DomainInvariantException("Order source and target departments must differ.");
        }

        OrganizationId = organizationId;
        BranchId = branchId;
        Name = Guard.Required(name, nameof(name), 200);
        SourceDepartmentId = sourceDepartmentId;
        TargetDepartmentId = targetDepartmentId;
        CreatedBy = createdBy;
    }

    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid SourceDepartmentId { get; set; }
    public Guid TargetDepartmentId { get; set; }
    public bool RequiresApproval { get; set; }
    public bool AllowCustomItems { get; set; }
    public Guid CreatedBy { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class OrderTemplateItem : TenantAuditableEntity
{
    public Guid OrderTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public UnitCode UnitCode { get; set; }
    public string? CustomUnitLabel { get; set; }
    public decimal? DefaultQuantity { get; set; }
    public decimal? MinimumQuantity { get; set; }
    public int SortOrder { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public void Validate()
    {
        Name = Guard.Required(Name, nameof(Name), 200);
        if (UnitCode == UnitCode.Custom && string.IsNullOrWhiteSpace(CustomUnitLabel))
        {
            throw new DomainInvariantException("A custom unit label is required for a custom unit.");
        }

        if (DefaultQuantity < 0 || MinimumQuantity < 0)
        {
            throw new DomainInvariantException("Template quantities cannot be negative.");
        }
    }
}

public sealed class DepartmentOrder : TenantSoftDeletableEntity
{
    private DepartmentOrder() { }

    public DepartmentOrder(
        Guid organizationId,
        Guid branchId,
        string orderNumber,
        Guid sourceDepartmentId,
        Guid targetDepartmentId,
        Guid createdBy,
        DateTimeOffset requestedAt)
    {
        if (sourceDepartmentId == targetDepartmentId)
        {
            throw new DomainInvariantException("Order source and target departments must differ.");
        }

        OrganizationId = organizationId;
        BranchId = branchId;
        OrderNumber = Guard.Required(orderNumber, nameof(orderNumber), 64);
        SourceDepartmentId = sourceDepartmentId;
        TargetDepartmentId = targetDepartmentId;
        CreatedBy = createdBy;
        RequestedAt = requestedAt;
    }

    public Guid BranchId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid? OrderTemplateId { get; set; }
    public Guid SourceDepartmentId { get; set; }
    public Guid TargetDepartmentId { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? AssignedTo { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public DepartmentOrderStatus Status { get; private set; } = DepartmentOrderStatus.Draft;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? RequiredAt { get; set; }
    public string? GeneralNote { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public Guid? AcceptedBy { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public Guid? DeliveredBy { get; private set; }
    public DateTimeOffset? ReceivedAt { get; private set; }
    public Guid? ReceivedBy { get; private set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public Guid? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? LinkedTaskId { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public void Submit()
    {
        RequireStatus(DepartmentOrderStatus.Draft, DepartmentOrderStatus.Submitted);
        Status = DepartmentOrderStatus.Submitted;
    }

    public void Accept(Guid actorId, DateTimeOffset acceptedAt)
    {
        RequireStatus(DepartmentOrderStatus.Submitted, DepartmentOrderStatus.Accepted);
        Status = DepartmentOrderStatus.Accepted;
        AcceptedAt = acceptedAt;
        AcceptedBy = actorId;
    }

    public void StartPreparing()
    {
        RequireStatus(DepartmentOrderStatus.Accepted, DepartmentOrderStatus.Preparing);
        Status = DepartmentOrderStatus.Preparing;
    }

    public void MarkReady(DateTimeOffset readyAt)
    {
        if (Status is not (DepartmentOrderStatus.Accepted or DepartmentOrderStatus.Preparing))
        {
            throw new InvalidStateTransitionException(nameof(DepartmentOrder), Status.ToString(), DepartmentOrderStatus.Ready.ToString());
        }

        Status = DepartmentOrderStatus.Ready;
        ReadyAt = readyAt;
    }

    public void Reject(Guid actorId, DateTimeOffset rejectedAt, string reason)
    {
        if (Status is not (DepartmentOrderStatus.Submitted or DepartmentOrderStatus.Accepted or DepartmentOrderStatus.Preparing))
        {
            throw new InvalidStateTransitionException(nameof(DepartmentOrder), Status.ToString(), DepartmentOrderStatus.Rejected.ToString());
        }

        Status = DepartmentOrderStatus.Rejected;
        RejectedAt = rejectedAt;
        RejectedBy = actorId;
        RejectionReason = Guard.Required(reason, nameof(reason), 1000);
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status is not (DepartmentOrderStatus.Draft or DepartmentOrderStatus.Submitted))
        {
            throw new InvalidStateTransitionException(nameof(DepartmentOrder), Status.ToString(), DepartmentOrderStatus.Cancelled.ToString());
        }

        Status = DepartmentOrderStatus.Cancelled;
        CancelledAt = cancelledAt;
    }

    public void MarkDelivered(Guid actorId, DateTimeOffset deliveredAt)
    {
        if (Status is not (DepartmentOrderStatus.Ready or DepartmentOrderStatus.Preparing or DepartmentOrderStatus.Accepted))
        {
            throw new InvalidStateTransitionException(nameof(DepartmentOrder), Status.ToString(), DepartmentOrderStatus.Delivered.ToString());
        }

        Status = DepartmentOrderStatus.Delivered;
        DeliveredAt = deliveredAt;
        DeliveredBy = actorId;
    }

    public void ConfirmReceipt(Guid actorId, DateTimeOffset receivedAt)
    {
        if (Status != DepartmentOrderStatus.Delivered || DeliveredAt is null)
        {
            throw new DomainInvariantException("Receipt cannot be confirmed before delivery.");
        }

        if (receivedAt < DeliveredAt)
        {
            throw new DomainInvariantException("Receipt time cannot precede delivery time.");
        }

        Status = DepartmentOrderStatus.Received;
        ReceivedAt = receivedAt;
        ReceivedBy = actorId;
    }

    private void RequireStatus(DepartmentOrderStatus expected, DepartmentOrderStatus target)
    {
        if (Status != expected)
        {
            throw new InvalidStateTransitionException(nameof(DepartmentOrder), Status.ToString(), target.ToString());
        }
    }
}

public sealed class DepartmentOrderItem : TenantAuditableEntity
{
    public Guid DepartmentOrderId { get; set; }
    public Guid? TemplateItemId { get; set; }
    public string ItemNameSnapshot { get; set; } = string.Empty;
    public string? ItemDescriptionSnapshot { get; set; }
    public UnitCode UnitCodeSnapshot { get; set; }
    public string? CustomUnitLabelSnapshot { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal FulfilledQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public DepartmentOrderItemStatus Status { get; set; }
    public string? ItemNote { get; set; }
    public string? FulfillmentNote { get; set; }
    public bool IsCustomItem { get; set; }
    public Guid? PreparedBy { get; set; }
    public DateTimeOffset? PreparedAt { get; set; }

    public void ValidateQuantities()
    {
        if (RequestedQuantity < 0 || FulfilledQuantity < 0 || ReceivedQuantity < 0)
        {
            throw new DomainInvariantException("Order item quantities cannot be negative.");
        }

        if (UnitCodeSnapshot == UnitCode.Custom && string.IsNullOrWhiteSpace(CustomUnitLabelSnapshot))
        {
            throw new DomainInvariantException("A custom unit label snapshot is required for a custom unit.");
        }
    }
}

public sealed class DepartmentOrderAttachment : TenantAuditableEntity
{
    public Guid DepartmentOrderId { get; set; }
    public Guid? OrderItemId { get; set; }
    public Guid UploadedBy { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string? Caption { get; set; }
}

public sealed class DepartmentOrderStatusHistory : TenantAuditableEntity
{
    public Guid DepartmentOrderId { get; set; }
    public DepartmentOrderStatus? OldStatus { get; set; }
    public DepartmentOrderStatus NewStatus { get; set; }
    public Guid ChangedBy { get; set; }
    public string? Note { get; set; }
}
