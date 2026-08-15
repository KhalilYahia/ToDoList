using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Tasks.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Tasks;

public interface ITaskTemplateService
{
    Task<PagedResponse<TaskTemplateDto>> ListAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task<TaskTemplateDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskTemplateDto> CreateAsync(SaveTaskTemplateRequest request, CancellationToken cancellationToken = default);
    Task<TaskTemplateDto> UpdateAsync(Guid id, SaveTaskTemplateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskTemplateDto> CloneAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<ChecklistDefinitionDto> AddItemAsync(Guid templateId, ChecklistDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<ChecklistDefinitionDto> UpdateItemAsync(Guid templateId, Guid itemId, ChecklistDefinitionRequest request, CancellationToken cancellationToken = default);
    Task DeleteItemAsync(Guid templateId, Guid itemId, CancellationToken cancellationToken = default);
    Task ReorderItemsAsync(Guid templateId, ReorderItemsRequest request, CancellationToken cancellationToken = default);
    Task<StoredFile> AddInstructionAsync(
        Guid templateId,
        Guid itemId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}

public sealed class TaskTemplateService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    ISubscriptionAccessService subscriptionAccess,
    IAuditService auditService,
    IFileStorageService fileStorage,
    IRequestValidator<SaveTaskTemplateRequest> validator) : ITaskTemplateService
{
    public async Task<PagedResponse<TaskTemplateDto>> ListAsync(
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        IReadOnlyList<Guid>? departments = await GetReadableDepartmentIdsAsync(organizationId, cancellationToken);
        PagedResult<TaskTemplate> result = await unitOfWork.Repository<TaskTemplate>().ListAsync(
            template => template.OrganizationId == organizationId &&
                (departments == null ||
                    template.DefaultDepartmentId.HasValue &&
                    departments.Contains(template.DefaultDepartmentId.Value)),
            page.ToDomain(),
            cancellationToken);
        List<TaskTemplateDto> mapped = [];
        foreach (TaskTemplate template in result.Items)
        {
            mapped.Add(await MapAsync(template, cancellationToken));
        }

        return new PagedResponse<TaskTemplateDto>(mapped, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<TaskTemplateDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TaskTemplate template = await GetAuthorizedAsync(id, cancellationToken);
        return await MapAsync(template, cancellationToken);
    }

    public async Task<TaskTemplateDto> CreateAsync(
        SaveTaskTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        Guid userId = currentUser.UserId!.Value;
        await subscriptionAccess.EnsureWriteAllowedAsync(
            organizationId,
            OpsManager.Domain.Constants.SubscriptionFeatureKeys.Tasks,
            cancellationToken);
        validator.ValidateAndThrow(request);
        await ValidateScopeAsync(organizationId, request.DefaultDepartmentId, cancellationToken);

        TaskTemplate template = new(organizationId, request.DefaultDepartmentId, request.Title, userId);
        Apply(template, request);
        await unitOfWork.Repository<TaskTemplate>().AddAsync(template, cancellationToken);
        await AddItemsAsync(template, request.Items, cancellationToken);
        await auditService.RecordTenantAsync(
            organizationId,
            "task-template.created",
            nameof(TaskTemplate),
            template.Id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(template, cancellationToken);
    }

    public async Task<TaskTemplateDto> UpdateAsync(
        Guid id,
        SaveTaskTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        validator.ValidateAndThrow(request);
        TaskTemplate template = await GetEntityAsync(id, cancellationToken);
        await ValidateScopeAsync(organizationId, request.DefaultDepartmentId, cancellationToken);
        Apply(template, request);
        unitOfWork.Repository<TaskTemplate>().Update(template);

        PagedResult<TaskTemplateItem> existing = await unitOfWork.Repository<TaskTemplateItem>().ListAsync(
            item => item.TaskTemplateId == id && item.IsActive,
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);
        foreach (TaskTemplateItem item in existing.Items)
        {
            item.Deactivate();
            unitOfWork.Repository<TaskTemplateItem>().Update(item);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await AddItemsAsync(template, request.Items, cancellationToken);
        await auditService.RecordTenantAsync(
            organizationId,
            "task-template.updated",
            nameof(TaskTemplate),
            template.Id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(template, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        TaskTemplate template = await GetEntityAsync(id, cancellationToken);
        unitOfWork.Repository<TaskTemplate>().Remove(template);
        await auditService.RecordTenantAsync(
            organizationId,
            "task-template.deleted",
            nameof(TaskTemplate),
            id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<TaskTemplateDto> CloneAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        TaskTemplate source = await GetEntityAsync(id, cancellationToken);
        PagedResult<TaskTemplateItem> sourceItems = await GetItemsAsync(source.Id, cancellationToken);
        TaskTemplate clone = new(organizationId, source.DefaultDepartmentId, $"{source.Title} (Copy)", currentUser.UserId!.Value);
        clone.Update(
            source.DefaultDepartmentId,
            $"{source.Title} (Copy)",
            source.Description,
            source.DefaultPriority,
            source.DefaultDurationMinutes,
            source.RequiresApproval);
        clone.Deactivate();
        await unitOfWork.Repository<TaskTemplate>().AddAsync(clone, cancellationToken);
        await unitOfWork.Repository<TaskTemplateItem>().AddRangeAsync(
            sourceItems.Items.Select(item => new TaskTemplateItem(
                organizationId,
                clone.Id,
                item.Title,
                item.SortOrder,
                item.Description,
                item.IsRequired,
                item.EvidenceMode,
                item.ItemType,
                item.Options,
                item.MainBlockTitle,
                item.SubBlockTitle,
                item.MaxAttachments)),
            cancellationToken);
        await auditService.RecordTenantAsync(
            organizationId,
            "task-template.cloned",
            nameof(TaskTemplate),
            clone.Id,
            newValues: new Dictionary<string, string>(StringComparer.Ordinal) { ["sourceId"] = source.Id.ToString() },
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(clone, cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        TaskTemplate template = await GetEntityAsync(id, cancellationToken);
        if (isActive)
        {
            template.Activate();
        }
        else
        {
            template.Deactivate();
        }
        unitOfWork.Repository<TaskTemplate>().Update(template);
        await auditService.RecordTenantAsync(
            organizationId,
            isActive ? "task-template.activated" : "task-template.deactivated",
            nameof(TaskTemplate),
            id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ChecklistDefinitionDto> AddItemAsync(
        Guid templateId,
        ChecklistDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        _ = await GetEntityAsync(templateId, cancellationToken);
        ValidateItem(request);
        TaskTemplateItem item = CreateItem(organizationId, templateId, request);
        await unitOfWork.Repository<TaskTemplateItem>().AddAsync(item, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public async Task<ChecklistDefinitionDto> UpdateItemAsync(
        Guid templateId,
        Guid itemId,
        ChecklistDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        _ = await GetEntityAsync(templateId, cancellationToken);
        ValidateItem(request);
        TaskTemplateItem item = await unitOfWork.Repository<TaskTemplateItem>()
            .FirstOrDefaultAsync(
                entity => entity.Id == itemId && entity.TaskTemplateId == templateId && entity.IsActive,
                cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskTemplateItem));
        Apply(item, request);
        unitOfWork.Repository<TaskTemplateItem>().Update(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public async Task DeleteItemAsync(Guid templateId, Guid itemId, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        _ = await GetEntityAsync(templateId, cancellationToken);
        TaskTemplateItem item = await unitOfWork.Repository<TaskTemplateItem>()
            .FirstOrDefaultAsync(
                entity => entity.Id == itemId && entity.TaskTemplateId == templateId && entity.IsActive,
                cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskTemplateItem));
        item.Deactivate();
        unitOfWork.Repository<TaskTemplateItem>().Update(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderItemsAsync(
        Guid templateId,
        ReorderItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        _ = await GetEntityAsync(templateId, cancellationToken);
        PagedResult<TaskTemplateItem> items = await GetItemsAsync(templateId, cancellationToken);
        if (request.ItemIds.Count != items.Items.Count ||
            request.ItemIds.Distinct().Count() != items.Items.Count ||
            items.Items.Any(item => !request.ItemIds.Contains(item.Id)))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.ItemIds)] = ["ItemIds must contain every template item exactly once."],
            });
        }

        Dictionary<Guid, int> order = request.ItemIds
            .Select((itemId, index) => (itemId, index))
            .ToDictionary(pair => pair.itemId, pair => pair.index);
        foreach (TaskTemplateItem item in items.Items)
        {
            item.Reorder(order[item.Id]);
            unitOfWork.Repository<TaskTemplateItem>().Update(item);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoredFile> AddInstructionAsync(
        Guid templateId,
        Guid itemId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        _ = await GetEntityAsync(templateId, cancellationToken);
        _ = await unitOfWork.Repository<TaskTemplateItem>()
            .FirstOrDefaultAsync(
                item => item.Id == itemId && item.TaskTemplateId == templateId && item.IsActive,
                cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskTemplateItem));
        StoredFile file = await fileStorage.SaveAsync(content, fileName, contentType, cancellationToken);
        TaskTemplateItemAttachment attachment = new(
            organizationId,
            itemId,
            file.Url,
            file.ContentType,
            currentUser.UserId!.Value);
        await unitOfWork.Repository<TaskTemplateItemAttachment>().AddAsync(attachment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return file;
    }

    private async Task<TaskTemplate> GetAuthorizedAsync(Guid id, CancellationToken cancellationToken)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        TaskTemplate template = await GetEntityAsync(id, cancellationToken);
        IReadOnlyList<Guid>? departments = await GetReadableDepartmentIdsAsync(organizationId, cancellationToken);
        if (departments is not null &&
            (!template.DefaultDepartmentId.HasValue ||
                !departments.Contains(template.DefaultDepartmentId.Value)))
        {
            throw new EntityNotFoundException(nameof(TaskTemplate));
        }

        return template;
    }

    private async Task<TaskTemplate> GetEntityAsync(Guid id, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<TaskTemplate>().GetByIdAsync(id, cancellationToken)
        ?? throw new EntityNotFoundException(nameof(TaskTemplate));

    private async Task<IReadOnlyList<Guid>?> GetReadableDepartmentIdsAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (currentUser.OrganizationRole == OrganizationRole.Manager)
        {
            return null;
        }

        Guid userId = currentUser.UserId!.Value;
        IReadOnlyList<Guid> memberships = await unitOfWork.Repository<UserDepartment>().ProjectAsync(
            item => item.OrganizationId == organizationId && item.UserId == userId && item.LeftAt == null,
            item => item.DepartmentId,
            cancellationToken);
        if (currentUser.OrganizationRole != OrganizationRole.Supervisor)
        {
            return memberships;
        }

        IReadOnlyList<Guid> supervised = await unitOfWork.Repository<Department>().ProjectAsync(
            department => department.OrganizationId == organizationId && department.SupervisorUserId == userId && department.IsActive,
            department => department.Id,
            cancellationToken);
        return memberships.Concat(supervised).Distinct().ToArray();
    }

    private async Task ValidateScopeAsync(
        Guid organizationId,
        Guid? departmentId,
        CancellationToken cancellationToken)
    {
        if (departmentId.HasValue)
        {
            Department department = await unitOfWork.Repository<Department>().GetByIdAsync(departmentId.Value, cancellationToken)
                ?? throw new EntityNotFoundException(nameof(Department));
            if (department.OrganizationId != organizationId || !department.IsActive)
            {
                throw new RequestValidationException(new Dictionary<string, string[]>
                {
                    [nameof(departmentId)] = ["The department must be active and belong to the organization."],
                });
            }
        }

    }

    private async Task AddItemsAsync(
        TaskTemplate template,
        IReadOnlyList<ChecklistDefinitionRequest> requests,
        CancellationToken cancellationToken)
    {
        int sortOrder = 0;
        List<TaskTemplateItem> items = new();
        foreach (ChecklistDefinitionRequest request in requests)
        {
            ChecklistDefinitionRequest normalized = request with { SortOrder = sortOrder++ };
            items.Add(CreateItem(template.OrganizationId, template.Id, normalized));
        }
        await unitOfWork.Repository<TaskTemplateItem>().AddRangeAsync(items, cancellationToken);
    }

    private async Task<PagedResult<TaskTemplateItem>> GetItemsAsync(Guid templateId, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<TaskTemplateItem>().ListAsync(
            item => item.TaskTemplateId == templateId && item.IsActive,
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);

    private async Task<TaskTemplateDto> MapAsync(TaskTemplate template, CancellationToken cancellationToken)
    {
        PagedResult<TaskTemplateItem> items = await GetItemsAsync(template.Id, cancellationToken);
        return new TaskTemplateDto(
            template.Id,
            template.DefaultDepartmentId,
            template.Title,
            template.Description,
            template.DefaultPriority,
            template.DefaultDurationMinutes,
            template.RequiresApproval,
            template.IsActive,
            items.Items.OrderBy(item => item.SortOrder).Select(Map).ToArray());
    }

    private static TaskTemplateItem CreateItem(
        Guid organizationId,
        Guid templateId,
        ChecklistDefinitionRequest request)
    {
        ValidateItem(request);
        return new TaskTemplateItem(
            organizationId,
            templateId,
            request.Title,
            request.SortOrder,
            request.Description,
            request.IsRequired,
            request.EvidenceMode,
            request.ItemType,
            request.Options,
            request.MainBlockTitle,
            request.SubBlockTitle,
            request.MaxAttachments);
    }

    private static void Apply(TaskTemplate template, SaveTaskTemplateRequest request)
    {
        template.Update(
            request.DefaultDepartmentId,
            request.Title,
            request.Description,
            request.DefaultPriority,
            request.DefaultDurationMinutes,
            request.RequiresApproval);
        if (request.IsActive)
        {
            template.Activate();
        }
        else
        {
            template.Deactivate();
        }
    }

    private static void Apply(TaskTemplateItem item, ChecklistDefinitionRequest request)
    {
        item.Update(
            request.Title,
            request.Description,
            request.SortOrder,
            request.IsRequired,
            request.EvidenceMode,
            request.ItemType,
            request.Options,
            request.MainBlockTitle,
            request.SubBlockTitle,
            request.MaxAttachments);
    }

    private static void ValidateItem(ChecklistDefinitionRequest request)
    {
        SaveTaskTemplateValidator.ValidateItems([request]);
    }

    private static ChecklistDefinitionDto Map(TaskTemplateItem item) =>
        new(item.Id, item.Title, item.Description, item.SortOrder, item.IsRequired, item.EvidenceMode, item.ItemType, item.Options, item.MainBlockTitle, item.SubBlockTitle, item.MaxAttachments);
}
