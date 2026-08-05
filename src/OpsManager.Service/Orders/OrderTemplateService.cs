using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Orders.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Orders;

public interface IOrderTemplateService
{
    Task<PagedResponse<OrderTemplateDto>> ListAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task<OrderTemplateDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrderTemplateDto> CreateAsync(SaveOrderTemplateRequest request, CancellationToken cancellationToken = default);
    Task<OrderTemplateDto> UpdateAsync(Guid id, SaveOrderTemplateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrderTemplateDto> CloneAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default);
    Task<OrderTemplateItemDto> AddItemAsync(Guid id, OrderTemplateItemRequest request, CancellationToken cancellationToken = default);
    Task<OrderTemplateItemDto> UpdateItemAsync(Guid id, Guid itemId, OrderTemplateItemRequest request, CancellationToken cancellationToken = default);
    Task DeleteItemAsync(Guid id, Guid itemId, CancellationToken cancellationToken = default);
    Task ReorderAsync(Guid id, ReorderOrderItemsRequest request, CancellationToken cancellationToken = default);
}

public sealed class OrderTemplateService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    ISubscriptionAccessService subscriptionAccess,
    IAuditService auditService,
    IRequestValidator<SaveOrderTemplateRequest> validator) : IOrderTemplateService
{
    public async Task<PagedResponse<OrderTemplateDto>> ListAsync(PageQuery page, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        IReadOnlyList<Guid>? departments = await GetAllowedDepartmentsAsync(organizationId, cancellationToken);
        PagedResult<OrderTemplate> result = await unitOfWork.Repository<OrderTemplate>().ListAsync(
            template => template.OrganizationId == organizationId &&
                (departments == null || departments.Contains(template.SourceDepartmentId)),
            page.ToDomain(),
            cancellationToken);
        List<OrderTemplateDto> items = [];
        foreach (OrderTemplate template in result.Items)
        {
            items.Add(await MapAsync(template, cancellationToken));
        }

        return new PagedResponse<OrderTemplateDto>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<OrderTemplateDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        OrderTemplate template = await GetAuthorizedAsync(id, cancellationToken);
        return await MapAsync(template, cancellationToken);
    }

    public async Task<OrderTemplateDto> CreateAsync(
        SaveOrderTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        validator.ValidateAndThrow(request);
        await ValidateScopeAsync(request, cancellationToken);
        OrderTemplate template = new(
            organizationId,
            request.BranchId,
            request.Name,
            request.SourceDepartmentId,
            request.TargetDepartmentId,
            currentUser.UserId!.Value);
        Apply(template, request);
        await unitOfWork.Repository<OrderTemplate>().AddAsync(template, cancellationToken);
        await unitOfWork.Repository<OrderTemplateItem>().AddRangeAsync(
            request.Items.Select(item => CreateItem(organizationId, template.Id, item)),
            cancellationToken);
        await AuditAsync(template, "order-template.created", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(template, cancellationToken);
    }

    public async Task<OrderTemplateDto> UpdateAsync(
        Guid id,
        SaveOrderTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        validator.ValidateAndThrow(request);
        await ValidateScopeAsync(request, cancellationToken);
        OrderTemplate template = await GetEntityAsync(id, cancellationToken);
        Apply(template, request);
        unitOfWork.Repository<OrderTemplate>().Update(template);
        PagedResult<OrderTemplateItem> oldItems = await GetItemsAsync(id, cancellationToken);
        foreach (OrderTemplateItem item in oldItems.Items)
        {
            item.IsActive = false;
            unitOfWork.Repository<OrderTemplateItem>().Update(item);
        }

        await unitOfWork.Repository<OrderTemplateItem>().AddRangeAsync(
            request.Items.Select(item => CreateItem(organizationId, template.Id, item)),
            cancellationToken);
        await AuditAsync(template, "order-template.updated", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(template, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        OrderTemplate template = await GetEntityAsync(id, cancellationToken);
        unitOfWork.Repository<OrderTemplate>().Remove(template);
        await AuditAsync(template, "order-template.deleted", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<OrderTemplateDto> CloneAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        OrderTemplate source = await GetEntityAsync(id, cancellationToken);
        PagedResult<OrderTemplateItem> sourceItems = await GetItemsAsync(source.Id, cancellationToken);
        OrderTemplate clone = new(
            organizationId,
            source.BranchId,
            $"{source.Name} (Copy)",
            source.SourceDepartmentId,
            source.TargetDepartmentId,
            currentUser.UserId!.Value)
        {
            Description = source.Description,
            RequiresApproval = source.RequiresApproval,
            AllowCustomItems = source.AllowCustomItems,
            IsActive = false,
        };
        await unitOfWork.Repository<OrderTemplate>().AddAsync(clone, cancellationToken);
        await unitOfWork.Repository<OrderTemplateItem>().AddRangeAsync(
            sourceItems.Items.Select(item => new OrderTemplateItem
            {
                OrganizationId = organizationId,
                OrderTemplateId = clone.Id,
                Name = item.Name,
                Description = item.Description,
                UnitCode = item.UnitCode,
                CustomUnitLabel = item.CustomUnitLabel,
                DefaultQuantity = item.DefaultQuantity,
                MinimumQuantity = item.MinimumQuantity,
                SortOrder = item.SortOrder,
                ImageUrl = item.ImageUrl,
                IsActive = item.IsActive,
            }),
            cancellationToken);
        await AuditAsync(clone, "order-template.cloned", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(clone, cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        OrderTemplate template = await GetEntityAsync(id, cancellationToken);
        template.IsActive = active;
        unitOfWork.Repository<OrderTemplate>().Update(template);
        await AuditAsync(template, active ? "order-template.activated" : "order-template.deactivated", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<OrderTemplateItemDto> AddItemAsync(
        Guid id,
        OrderTemplateItemRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        _ = await GetEntityAsync(id, cancellationToken);
        OrderValidation.ValidateItem(request);
        OrderTemplateItem item = CreateItem(organizationId, id, request);
        await unitOfWork.Repository<OrderTemplateItem>().AddAsync(item, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public async Task<OrderTemplateItemDto> UpdateItemAsync(
        Guid id,
        Guid itemId,
        OrderTemplateItemRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        _ = await GetEntityAsync(id, cancellationToken);
        OrderValidation.ValidateItem(request);
        OrderTemplateItem item = await unitOfWork.Repository<OrderTemplateItem>()
            .FirstOrDefaultAsync(
                entity => entity.Id == itemId && entity.OrderTemplateId == id && entity.IsActive,
                cancellationToken)
            ?? throw new EntityNotFoundException(nameof(OrderTemplateItem));
        Apply(item, request);
        unitOfWork.Repository<OrderTemplateItem>().Update(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public async Task DeleteItemAsync(Guid id, Guid itemId, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        _ = await GetEntityAsync(id, cancellationToken);
        OrderTemplateItem item = await unitOfWork.Repository<OrderTemplateItem>()
            .FirstOrDefaultAsync(
                entity => entity.Id == itemId && entity.OrderTemplateId == id && entity.IsActive,
                cancellationToken)
            ?? throw new EntityNotFoundException(nameof(OrderTemplateItem));
        item.IsActive = false;
        unitOfWork.Repository<OrderTemplateItem>().Update(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderAsync(
        Guid id,
        ReorderOrderItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await EnsureWriteAsync(organizationId, cancellationToken);
        _ = await GetEntityAsync(id, cancellationToken);
        PagedResult<OrderTemplateItem> items = await GetItemsAsync(id, cancellationToken);
        if (request.ItemIds.Count != items.Items.Count ||
            request.ItemIds.Distinct().Count() != items.Items.Count ||
            items.Items.Any(item => !request.ItemIds.Contains(item.Id)))
        {
            throw OrderValidation.Validation(nameof(request.ItemIds), "ItemIds must contain every item exactly once.");
        }

        for (int index = 0; index < request.ItemIds.Count; index++)
        {
            OrderTemplateItem item = items.Items.Single(entity => entity.Id == request.ItemIds[index]);
            item.SortOrder = index;
            unitOfWork.Repository<OrderTemplateItem>().Update(item);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Task EnsureWriteAsync(Guid organizationId, CancellationToken cancellationToken) =>
        subscriptionAccess.EnsureWriteAllowedAsync(
            organizationId,
            OpsManager.Domain.Constants.SubscriptionFeatureKeys.DepartmentOrders,
            cancellationToken);

    private async Task<OrderTemplate> GetAuthorizedAsync(Guid id, CancellationToken cancellationToken)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        OrderTemplate template = await GetEntityAsync(id, cancellationToken);
        IReadOnlyList<Guid>? allowed = await GetAllowedDepartmentsAsync(organizationId, cancellationToken);
        if (allowed is not null && !allowed.Contains(template.SourceDepartmentId))
        {
            throw new EntityNotFoundException(nameof(OrderTemplate));
        }

        return template;
    }

    private async Task<OrderTemplate> GetEntityAsync(Guid id, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<OrderTemplate>().GetByIdAsync(id, cancellationToken)
        ?? throw new EntityNotFoundException(nameof(OrderTemplate));

    private async Task ValidateScopeAsync(SaveOrderTemplateRequest request, CancellationToken cancellationToken)
    {
        Branch branch = await unitOfWork.Repository<Branch>().GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Branch));
        Department source = await unitOfWork.Repository<Department>().GetByIdAsync(request.SourceDepartmentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Department));
        Department target = await unitOfWork.Repository<Department>().GetByIdAsync(request.TargetDepartmentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Department));
        if (!branch.IsActive || !source.IsActive || !target.IsActive ||
            source.BranchId != branch.Id || target.BranchId != branch.Id)
        {
            throw new ConflictException("Source and target departments must be active and belong to the selected branch.", "invalid_order_route");
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

    private async Task<PagedResult<OrderTemplateItem>> GetItemsAsync(Guid id, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<OrderTemplateItem>().ListAsync(
            item => item.OrderTemplateId == id && item.IsActive,
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);

    private async Task<OrderTemplateDto> MapAsync(OrderTemplate template, CancellationToken cancellationToken)
    {
        PagedResult<OrderTemplateItem> items = await GetItemsAsync(template.Id, cancellationToken);
        return new OrderTemplateDto(
            template.Id,
            template.BranchId,
            template.Name,
            template.Description,
            template.SourceDepartmentId,
            template.TargetDepartmentId,
            template.RequiresApproval,
            template.AllowCustomItems,
            template.IsActive,
            items.Items.OrderBy(item => item.SortOrder).Select(Map).ToArray());
    }

    private Task AuditAsync(OrderTemplate template, string action, CancellationToken cancellationToken) =>
        auditService.RecordTenantAsync(
            template.OrganizationId,
            action,
            nameof(OrderTemplate),
            template.Id,
            cancellationToken: cancellationToken);

    private static OrderTemplateItem CreateItem(Guid organizationId, Guid templateId, OrderTemplateItemRequest request)
    {
        OrderTemplateItem item = new()
        {
            OrganizationId = organizationId,
            OrderTemplateId = templateId,
        };
        Apply(item, request);
        item.Validate();
        return item;
    }

    private static void Apply(OrderTemplate template, SaveOrderTemplateRequest request)
    {
        template.BranchId = request.BranchId;
        template.Name = request.Name.Trim();
        template.Description = request.Description?.Trim();
        template.SourceDepartmentId = request.SourceDepartmentId;
        template.TargetDepartmentId = request.TargetDepartmentId;
        template.RequiresApproval = request.RequiresApproval;
        template.AllowCustomItems = request.AllowCustomItems;
        template.IsActive = request.IsActive;
    }

    private static void Apply(OrderTemplateItem item, OrderTemplateItemRequest request)
    {
        item.Name = request.Name.Trim();
        item.Description = request.Description?.Trim();
        item.UnitCode = request.UnitCode;
        item.CustomUnitLabel = request.CustomUnitLabel?.Trim();
        item.DefaultQuantity = request.DefaultQuantity;
        item.MinimumQuantity = request.MinimumQuantity;
        item.SortOrder = request.SortOrder;
        item.ImageUrl = request.ImageUrl?.Trim();
        item.IsActive = request.IsActive;
    }

    private static OrderTemplateItemDto Map(OrderTemplateItem item) =>
        new(
            item.Id,
            item.Name,
            item.Description,
            item.UnitCode,
            item.CustomUnitLabel,
            item.DefaultQuantity,
            item.MinimumQuantity,
            item.SortOrder,
            item.ImageUrl,
            item.IsActive);
}
