using System.Globalization;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Orders.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Orders;

public enum OrderListScope { All, Incoming, Outgoing }

public enum OrderAction
{
    Accept,
    Start,
    MarkReady,
    Deliver,
    ConfirmReceipt,
    Reject,
    Cancel,
}

public interface IDepartmentOrderService
{
    Task<PagedResponse<DepartmentOrderDto>> ListAsync(
        DepartmentOrderQuery query,
        OrderListScope scope,
        CancellationToken cancellationToken = default);
    Task<DepartmentOrderDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DepartmentOrderDto> CreateAsync(CreateDepartmentOrderRequest request, CancellationToken cancellationToken = default);
    Task<DepartmentOrderDto> CreateFromTemplateAsync(
        Guid templateId,
        CreateOrderFromTemplateRequest request,
        CancellationToken cancellationToken = default);
    Task<DepartmentOrderDto> AssignAsync(Guid id, AssignOrderRequest request, CancellationToken cancellationToken = default);
    Task<DepartmentOrderDto> ActAsync(Guid id, OrderAction action, string? reason, CancellationToken cancellationToken = default);
    Task<DepartmentOrderItemDto> UpdateItemAsync(
        Guid id,
        Guid itemId,
        UpdateOrderItemRequest request,
        CancellationToken cancellationToken = default);
    Task<StoredFile> AddAttachmentAsync(
        Guid id,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}

public sealed class DepartmentOrderService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    ISubscriptionAccessService subscriptionAccess,
    IAuditService auditService,
    INotificationService notifications,
    IFileStorageService fileStorage,
    IClock clock,
    IRequestValidator<CreateDepartmentOrderRequest> validator) : IDepartmentOrderService
{
    public async Task<PagedResponse<DepartmentOrderDto>> ListAsync(
        DepartmentOrderQuery query,
        OrderListScope scope,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        IReadOnlyList<Guid>? departments = await GetAllowedDepartmentsAsync(organizationId, cancellationToken);
        DateTimeOffset now = clock.UtcNow;
        PagedResult<DepartmentOrder> result = await unitOfWork.Repository<DepartmentOrder>().ListAsync(
            order => order.OrganizationId == organizationId &&
                (departments == null ||
                    departments.Contains(order.SourceDepartmentId) ||
                    departments.Contains(order.TargetDepartmentId)) &&
                (scope != OrderListScope.Incoming || departments == null || departments.Contains(order.TargetDepartmentId)) &&
                (scope != OrderListScope.Outgoing || departments == null || departments.Contains(order.SourceDepartmentId)) &&
                (!query.From.HasValue || order.RequestedAt >= query.From.Value) &&
                (!query.To.HasValue || order.RequestedAt < query.To.Value) &&
                (!query.Status.HasValue || order.Status == query.Status.Value) &&
                (!query.SourceDepartmentId.HasValue || order.SourceDepartmentId == query.SourceDepartmentId.Value) &&
                (!query.TargetDepartmentId.HasValue || order.TargetDepartmentId == query.TargetDepartmentId.Value) &&
                (!query.AssigneeUserId.HasValue || order.AssignedTo == query.AssigneeUserId.Value) &&
                (!query.Priority.HasValue || order.Priority == query.Priority.Value) &&
                (!query.Late.HasValue ||
                    query.Late.Value ==
                    (order.RequiredAt.HasValue &&
                        order.RequiredAt.Value < now &&
                        order.Status != DepartmentOrderStatus.Delivered &&
                        order.Status != DepartmentOrderStatus.Received &&
                        order.Status != DepartmentOrderStatus.Rejected &&
                        order.Status != DepartmentOrderStatus.Cancelled)),
            query.PageQuery.ToDomain(),
            cancellationToken);
        List<DepartmentOrderDto> items = [];
        foreach (DepartmentOrder order in result.Items)
        {
            items.Add(await MapAsync(order, cancellationToken));
        }

        return new PagedResponse<DepartmentOrderDto>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<DepartmentOrderDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await MapAsync(await GetAuthorizedAsync(id, cancellationToken), cancellationToken);

    public async Task<DepartmentOrderDto> CreateAsync(
        CreateDepartmentOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        validator.ValidateAndThrow(request);
        await EnsureSourceAccessAsync(organizationId, request.SourceDepartmentId, cancellationToken);
        await ValidateRouteAsync(request.BranchId, request.SourceDepartmentId, request.TargetDepartmentId, cancellationToken);
        OrderTemplate? template = request.OrderTemplateId.HasValue
            ? await unitOfWork.Repository<OrderTemplate>().GetByIdAsync(request.OrderTemplateId.Value, cancellationToken)
            : null;
        if (request.OrderTemplateId.HasValue && template is null)
        {
            throw new EntityNotFoundException(nameof(OrderTemplate));
        }

        return await CreateInternalAsync(request, template, cancellationToken);
    }

    public async Task<DepartmentOrderDto> CreateFromTemplateAsync(
        Guid templateId,
        CreateOrderFromTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        OrderTemplate template = await unitOfWork.Repository<OrderTemplate>().GetByIdAsync(templateId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(OrderTemplate));
        if (!template.IsActive)
        {
            throw new ConflictException("The order template is inactive.", "inactive_order_template");
        }

        CreateDepartmentOrderRequest create = new(
            template.Id,
            template.BranchId,
            template.SourceDepartmentId,
            template.TargetDepartmentId,
            request.RequiredAt,
            request.Priority,
            request.GeneralNote,
            request.Items);
        validator.ValidateAndThrow(create);
        await EnsureSourceAccessAsync(organizationId, template.SourceDepartmentId, cancellationToken);
        return await CreateInternalAsync(create, template, cancellationToken);
    }

    public async Task<DepartmentOrderDto> AssignAsync(
        Guid id,
        AssignOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        DepartmentOrder order = await GetAuthorizedAsync(id, cancellationToken);
        await EnsureTargetAccessAsync(organizationId, order.TargetDepartmentId, cancellationToken);
        bool activeMember = await unitOfWork.Repository<OrganizationMember>().AnyAsync(
            member => member.UserId == request.AssigneeUserId && member.IsActive,
            cancellationToken);
        if (!activeMember)
        {
            throw OrderValidation.Validation(nameof(request.AssigneeUserId), "Assignee must be an active member.");
        }

        order.AssignedTo = request.AssigneeUserId;
        unitOfWork.Repository<DepartmentOrder>().Update(order);
        await AuditAsync(order, "department-order.assigned", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await TryNotifyAsync(order, request.AssigneeUserId, "Order assigned", cancellationToken);
        return await MapAsync(order, cancellationToken);
    }

    public async Task<DepartmentOrderDto> ActAsync(
        Guid id,
        OrderAction action,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        DepartmentOrder order = await GetAuthorizedAsync(id, cancellationToken);
        DepartmentOrderStatus oldStatus = order.Status;
        switch (action)
        {
            case OrderAction.Accept:
                await EnsureTargetAccessAsync(organizationId, order.TargetDepartmentId, cancellationToken);
                order.Accept(currentUser.UserId!.Value, clock.UtcNow);
                break;
            case OrderAction.Start:
                await EnsureTargetAccessAsync(organizationId, order.TargetDepartmentId, cancellationToken);
                order.StartPreparing();
                break;
            case OrderAction.MarkReady:
                await EnsureTargetAccessAsync(organizationId, order.TargetDepartmentId, cancellationToken);
                await EnsureItemsReadyAsync(order.Id, cancellationToken);
                order.MarkReady(clock.UtcNow);
                break;
            case OrderAction.Deliver:
                await EnsureTargetAccessAsync(organizationId, order.TargetDepartmentId, cancellationToken);
                await EnsureItemsReadyAsync(order.Id, cancellationToken);
                order.MarkDelivered(currentUser.UserId!.Value, clock.UtcNow);
                break;
            case OrderAction.ConfirmReceipt:
                await EnsureSourceAccessAsync(organizationId, order.SourceDepartmentId, cancellationToken);
                await ValidateReceiptQuantitiesAsync(order.Id, cancellationToken);
                order.ConfirmReceipt(currentUser.UserId!.Value, clock.UtcNow);
                break;
            case OrderAction.Reject:
                await EnsureTargetAccessAsync(organizationId, order.TargetDepartmentId, cancellationToken);
                order.Reject(currentUser.UserId!.Value, clock.UtcNow, reason ?? string.Empty);
                break;
            case OrderAction.Cancel:
                await EnsureSourceAccessAsync(organizationId, order.SourceDepartmentId, cancellationToken);
                order.Cancel(clock.UtcNow);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }

        unitOfWork.Repository<DepartmentOrder>().Update(order);
        await AddHistoryAsync(order, oldStatus, reason, cancellationToken);
        await AuditAsync(order, $"department-order.{action.ToString().ToLowerInvariant()}", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (action is OrderAction.Accept or OrderAction.MarkReady or OrderAction.Deliver)
        {
            await TryNotifyAsync(order, order.CreatedBy, $"Order {action}", cancellationToken);
        }

        return await MapAsync(order, cancellationToken);
    }

    public async Task<DepartmentOrderItemDto> UpdateItemAsync(
        Guid id,
        Guid itemId,
        UpdateOrderItemRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        DepartmentOrder order = await GetAuthorizedAsync(id, cancellationToken);
        bool receiptPhase = order.Status == DepartmentOrderStatus.Delivered;
        if (receiptPhase)
        {
            await EnsureSourceAccessAsync(organizationId, order.SourceDepartmentId, cancellationToken);
        }
        else
        {
            await EnsureTargetAccessAsync(organizationId, order.TargetDepartmentId, cancellationToken);
        }

        if (order.Status is DepartmentOrderStatus.Received or DepartmentOrderStatus.Rejected or DepartmentOrderStatus.Cancelled)
        {
            throw new ConflictException("This order is immutable.", "order_immutable");
        }

        DepartmentOrderItem item = await unitOfWork.Repository<DepartmentOrderItem>().FirstOrDefaultAsync(
            entity => entity.Id == itemId && entity.DepartmentOrderId == id,
            cancellationToken)
            ?? throw new EntityNotFoundException(nameof(DepartmentOrderItem));
        if (request.FulfilledQuantity < 0 || request.ReceivedQuantity < 0 ||
            request.ReceivedQuantity > request.FulfilledQuantity)
        {
            throw OrderValidation.Validation("quantity", "Quantities must be non-negative and received cannot exceed fulfilled.");
        }

        item.FulfilledQuantity = request.FulfilledQuantity;
        item.ReceivedQuantity = request.ReceivedQuantity;
        item.Status = request.Status;
        item.FulfillmentNote = request.FulfillmentNote?.Trim();
        item.PreparedBy = currentUser.UserId;
        item.PreparedAt = clock.UtcNow;
        item.ValidateQuantities();
        unitOfWork.Repository<DepartmentOrderItem>().Update(item);
        await AuditAsync(order, "department-order.item-updated", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public async Task<StoredFile> AddAttachmentAsync(
        Guid id,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        _ = await GetAuthorizedAsync(id, cancellationToken);
        StoredFile file = await fileStorage.SaveAsync(content, fileName, contentType, cancellationToken);
        await unitOfWork.Repository<DepartmentOrderAttachment>().AddAsync(
            new DepartmentOrderAttachment
            {
                OrganizationId = organizationId,
                DepartmentOrderId = id,
                UploadedBy = currentUser.UserId!.Value,
                FileUrl = file.Url,
                FileType = file.ContentType,
            },
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return file;
    }

    private async Task<DepartmentOrderDto> CreateInternalAsync(
        CreateDepartmentOrderRequest request,
        OrderTemplate? template,
        CancellationToken cancellationToken)
    {
        Guid organizationId = currentUser.OrganizationId!.Value;
        IReadOnlyList<OrderTemplateItem> templateItems = template is null
            ? []
            : (await unitOfWork.Repository<OrderTemplateItem>().ListAsync(
                item => item.OrderTemplateId == template.Id && item.IsActive,
                new PageRequest(1, PageRequest.MaximumPageSize),
                cancellationToken)).Items;
        List<DepartmentOrderItem> items = [];
        foreach (CreateOrderItemRequest itemRequest in request.Items)
        {
            OrderTemplateItem? templateItem = itemRequest.TemplateItemId.HasValue
                ? templateItems.FirstOrDefault(item => item.Id == itemRequest.TemplateItemId.Value)
                : null;
            bool custom = templateItem is null;
            if (itemRequest.TemplateItemId.HasValue && templateItem is null)
            {
                throw OrderValidation.Validation(nameof(itemRequest.TemplateItemId), "Selected template item is unavailable.");
            }

            if (custom && template is not null && !template.AllowCustomItems)
            {
                throw new ForbiddenAccessException("This template does not permit custom items.");
            }

            string name = templateItem?.Name ?? itemRequest.CustomName?.Trim() ?? string.Empty;
            UnitCode unit = templateItem?.UnitCode ?? itemRequest.UnitCode ?? UnitCode.Each;
            string? customUnit = templateItem?.CustomUnitLabel ?? itemRequest.CustomUnitLabel?.Trim();
            if (string.IsNullOrWhiteSpace(name) || unit == UnitCode.Custom && string.IsNullOrWhiteSpace(customUnit))
            {
                throw OrderValidation.Validation(nameof(request.Items), "Custom items require a name and custom units require a label.");
            }

            if (templateItem?.MinimumQuantity is decimal minimum && itemRequest.RequestedQuantity < minimum)
            {
                throw OrderValidation.Validation(nameof(itemRequest.RequestedQuantity), $"Requested quantity must be at least {minimum}.");
            }

            DepartmentOrderItem item = new()
            {
                OrganizationId = organizationId,
                TemplateItemId = templateItem?.Id,
                ItemNameSnapshot = name,
                ItemDescriptionSnapshot = templateItem?.Description ?? itemRequest.Description?.Trim(),
                UnitCodeSnapshot = unit,
                CustomUnitLabelSnapshot = customUnit,
                RequestedQuantity = itemRequest.RequestedQuantity,
                ItemNote = itemRequest.Note?.Trim(),
                IsCustomItem = custom,
            };
            item.ValidateQuantities();
            items.Add(item);
        }

        DateTimeOffset now = clock.UtcNow;
        string number = string.Create(
            CultureInfo.InvariantCulture,
            $"ORD-{now:yyyyMMdd}-{Guid.NewGuid():N}");
        DepartmentOrder order = new(
            organizationId,
            request.BranchId,
            number,
            request.SourceDepartmentId,
            request.TargetDepartmentId,
            currentUser.UserId!.Value,
            now)
        {
            OrderTemplateId = template?.Id,
            RequiredAt = request.RequiredAt,
            Priority = request.Priority,
            GeneralNote = request.GeneralNote?.Trim(),
        };
        order.Submit();
        foreach (DepartmentOrderItem item in items)
        {
            item.DepartmentOrderId = order.Id;
        }

        await unitOfWork.Repository<DepartmentOrder>().AddAsync(order, cancellationToken);
        await unitOfWork.Repository<DepartmentOrderItem>().AddRangeAsync(items, cancellationToken);
        await AddHistoryAsync(order, null, "Order submitted.", cancellationToken);
        await AuditAsync(order, "department-order.created", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(order, cancellationToken);
    }

    private async Task<DepartmentOrder> GetAuthorizedAsync(Guid id, CancellationToken cancellationToken)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        DepartmentOrder order = await unitOfWork.Repository<DepartmentOrder>().GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(DepartmentOrder));
        IReadOnlyList<Guid>? departments = await GetAllowedDepartmentsAsync(organizationId, cancellationToken);
        if (departments is not null &&
            !departments.Contains(order.SourceDepartmentId) &&
            !departments.Contains(order.TargetDepartmentId))
        {
            throw new EntityNotFoundException(nameof(DepartmentOrder));
        }

        return order;
    }

    private async Task EnsureSourceAccessAsync(Guid organizationId, Guid departmentId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid>? departments = await GetAllowedDepartmentsAsync(organizationId, cancellationToken);
        if (departments is not null && !departments.Contains(departmentId))
        {
            throw new ForbiddenAccessException("Source-department access is required.");
        }
    }

    private async Task EnsureTargetAccessAsync(Guid organizationId, Guid departmentId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid>? departments = await GetAllowedDepartmentsAsync(organizationId, cancellationToken);
        if (departments is not null && !departments.Contains(departmentId))
        {
            throw new ForbiddenAccessException("Target-department access is required.");
        }
    }

    private async Task<IReadOnlyList<Guid>?> GetAllowedDepartmentsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (currentUser.OrganizationRole == OrganizationRole.Manager)
        {
            return null;
        }

        Guid userId = currentUser.UserId!.Value;
        IReadOnlyList<Guid> memberships = await unitOfWork.Repository<UserDepartment>().ProjectAsync(
            relation => relation.OrganizationId == organizationId && relation.UserId == userId && relation.LeftAt == null,
            relation => relation.DepartmentId,
            cancellationToken);
        if (currentUser.OrganizationRole != OrganizationRole.Supervisor)
        {
            return memberships;
        }

        IReadOnlyList<Guid> supervised = await unitOfWork.Repository<Department>().ProjectAsync(
            department => department.SupervisorUserId == userId && department.IsActive,
            department => department.Id,
            cancellationToken);
        return memberships.Concat(supervised).Distinct().ToArray();
    }

    private async Task ValidateRouteAsync(
        Guid branchId,
        Guid sourceDepartmentId,
        Guid targetDepartmentId,
        CancellationToken cancellationToken)
    {
        Branch branch = await unitOfWork.Repository<Branch>().GetByIdAsync(branchId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Branch));
        Department source = await unitOfWork.Repository<Department>().GetByIdAsync(sourceDepartmentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Department));
        Department target = await unitOfWork.Repository<Department>().GetByIdAsync(targetDepartmentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Department));
        if (!branch.IsActive || !source.IsActive || !target.IsActive ||
            source.BranchId != branchId || target.BranchId != branchId)
        {
            throw new ConflictException("Order departments must be active and belong to the selected branch.", "invalid_order_route");
        }
    }

    private async Task EnsureItemsReadyAsync(Guid orderId, CancellationToken cancellationToken)
    {
        bool notReady = await unitOfWork.Repository<DepartmentOrderItem>().AnyAsync(
            item => item.DepartmentOrderId == orderId &&
                item.Status != DepartmentOrderItemStatus.Ready &&
                item.Status != DepartmentOrderItemStatus.Fulfilled &&
                item.Status != DepartmentOrderItemStatus.PartiallyFulfilled &&
                item.Status != DepartmentOrderItemStatus.Rejected,
            cancellationToken);
        if (notReady)
        {
            throw new ConflictException(
                "Every item must be ready, fulfilled, partially fulfilled, or explicitly unavailable/rejected.",
                "order_items_not_ready");
        }
    }

    private async Task ValidateReceiptQuantitiesAsync(Guid orderId, CancellationToken cancellationToken)
    {
        bool invalid = await unitOfWork.Repository<DepartmentOrderItem>().AnyAsync(
            item => item.DepartmentOrderId == orderId &&
                (item.ReceivedQuantity < 0 || item.ReceivedQuantity > item.FulfilledQuantity),
            cancellationToken);
        if (invalid)
        {
            throw new ConflictException("Received quantities cannot exceed fulfilled quantities.", "invalid_received_quantity");
        }
    }

    private Task EnsureWriteAsync(Guid organizationId, CancellationToken cancellationToken) =>
        subscriptionAccess.EnsureWriteAllowedAsync(
            organizationId,
            OpsManager.Domain.Constants.SubscriptionFeatureKeys.DepartmentOrders,
            cancellationToken);

    private async Task AddHistoryAsync(
        DepartmentOrder order,
        DepartmentOrderStatus? oldStatus,
        string? note,
        CancellationToken cancellationToken)
    {
        await unitOfWork.Repository<DepartmentOrderStatusHistory>().AddAsync(
            new DepartmentOrderStatusHistory
            {
                OrganizationId = order.OrganizationId,
                DepartmentOrderId = order.Id,
                OldStatus = oldStatus,
                NewStatus = order.Status,
                ChangedBy = currentUser.UserId!.Value,
                Note = note?.Trim(),
            },
            cancellationToken);
    }

    private Task AuditAsync(DepartmentOrder order, string action, CancellationToken cancellationToken) =>
        auditService.RecordTenantAsync(
            order.OrganizationId,
            action,
            nameof(DepartmentOrder),
            order.Id,
            cancellationToken: cancellationToken);

    private async Task TryNotifyAsync(
        DepartmentOrder order,
        Guid recipientId,
        string title,
        CancellationToken cancellationToken)
    {
        try
        {
            await notifications.CreateAsync(
                order.OrganizationId,
                recipientId,
                NotificationType.OrderUpdated,
                title,
                order.OrderNumber,
                relatedEntityType: nameof(DepartmentOrder),
                relatedEntityId: order.Id,
                cancellationToken: cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Best-effort post-commit notification; the order mutation remains committed.
        }
    }

    private async Task<DepartmentOrderDto> MapAsync(DepartmentOrder order, CancellationToken cancellationToken)
    {
        PagedResult<DepartmentOrderItem> items = await unitOfWork.Repository<DepartmentOrderItem>().ListAsync(
            item => item.DepartmentOrderId == order.Id,
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);
        return new DepartmentOrderDto(
            order.Id,
            order.OrderNumber,
            order.OrderTemplateId,
            order.BranchId,
            order.SourceDepartmentId,
            order.TargetDepartmentId,
            order.CreatedBy,
            order.AssignedTo,
            order.Priority,
            order.Status,
            order.RequestedAt,
            order.RequiredAt,
            IsLate(order, clock.UtcNow),
            order.GeneralNote,
            order.AcceptedAt,
            order.ReadyAt,
            order.DeliveredAt,
            order.ReceivedAt,
            order.RejectionReason,
            items.Items.Select(Map).ToArray());
    }

    private static DepartmentOrderItemDto Map(DepartmentOrderItem item) =>
        new(
            item.Id,
            item.ItemNameSnapshot,
            item.ItemDescriptionSnapshot,
            item.UnitCodeSnapshot,
            item.CustomUnitLabelSnapshot,
            item.RequestedQuantity,
            item.FulfilledQuantity,
            item.ReceivedQuantity,
            item.Status,
            item.ItemNote,
            item.FulfillmentNote,
            item.IsCustomItem);

    private static bool IsLate(DepartmentOrder order, DateTimeOffset now) =>
        order.RequiredAt.HasValue &&
        order.RequiredAt.Value < now &&
        order.Status is not (
            DepartmentOrderStatus.Delivered or
            DepartmentOrderStatus.Received or
            DepartmentOrderStatus.Rejected or
            DepartmentOrderStatus.Cancelled);
}
