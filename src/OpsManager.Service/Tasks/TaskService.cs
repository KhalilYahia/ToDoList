using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Tasks.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Tasks;

public interface ITaskService
{
    Task<PagedResponse<TaskDto>> ListAsync(TaskQuery query, bool mineOnly = false, CancellationToken cancellationToken = default);
    Task<TaskDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskDistributionResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDistributionResponse> CreateFromTemplateAsync(Guid templateId, CreateTaskFromTemplateRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> AssignAsync(Guid id, AssignTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> StartAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskDto> BlockAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<TaskDto> ResumeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskDto> CompleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskDto> ReturnForCorrectionAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<TaskDto> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskDistributionResponse> CloneAsync(Guid id, CloneTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskItemDto> UpdateItemAsync(Guid taskId, Guid itemId, UpdateTaskItemRequest request, CancellationToken cancellationToken = default);
    Task<StoredFile> AddAttachmentAsync(
        Guid taskId,
        Guid itemId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
    Task DeleteAttachmentAsync(Guid taskId, Guid itemId, Guid attachmentId, CancellationToken cancellationToken = default);
}

public sealed class TaskService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    ISubscriptionAccessService subscriptionAccess,
    IAuditService auditService,
    INotificationService notifications,
    IFileStorageService fileStorage,
    IClock clock,
    IRequestValidator<CreateTaskRequest> validator,
    ITaskAssigneeResolver assigneeResolver,
    ITaskDistributionCreator distributionCreator) : ITaskService
{
    public async Task<PagedResponse<TaskDto>> ListAsync(
        TaskQuery query,
        bool mineOnly = false,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        Guid userId = currentUser.UserId!.Value;
        IReadOnlyList<Guid>? allowedDepartments = await GetAllowedDepartmentIdsAsync(organizationId, cancellationToken);
        bool employee = currentUser.OrganizationRole == OrganizationRole.Employee;
        bool isManager = currentUser.OrganizationRole == OrganizationRole.Manager;
        DateTimeOffset now = clock.UtcNow;

        Func<IQueryable<OperationalTask>, IOrderedQueryable<OperationalTask>> orderBy = query.Scope switch
        {
            TaskTemporalScope.Upcoming => q => q.OrderBy(t => t.ScheduledStartAt).ThenBy(t => t.DueAt),
            TaskTemporalScope.Past => q => q.OrderByDescending(t => t.DueAt),
            _ => q => q.OrderByDescending(t => t.ScheduledStartAt),
        };

        string? searchPattern = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        PagedResult<OperationalTask> result = await unitOfWork.Repository<OperationalTask>().ListAsync(
            task => task.OrganizationId == organizationId &&
                (!employee || task.AssigneeUserId == userId) &&
                (employee || allowedDepartments == null || allowedDepartments.Contains(task.DepartmentId)) &&
                (!mineOnly || task.AssigneeUserId == userId) &&
                (isManager || query.Status.HasValue || task.Status != OperationalTaskStatus.Cancelled) &&
                (!query.Scope.HasValue || (query.Scope == TaskTemporalScope.Upcoming
                    ? (task.Status != OperationalTaskStatus.Completed && task.Status != OperationalTaskStatus.Cancelled && task.DueAt >= now)
                    : (task.Status == OperationalTaskStatus.Completed || task.Status == OperationalTaskStatus.Cancelled || task.DueAt < now))) &&
                (searchPattern == null || task.Title.Contains(searchPattern, StringComparison.OrdinalIgnoreCase)) &&
                (!query.From.HasValue || task.ScheduledStartAt >= query.From.Value) &&
                (!query.To.HasValue || task.ScheduledStartAt <= query.To.Value) &&
                (!query.Status.HasValue || task.Status == query.Status.Value) &&
                (!query.BranchId.HasValue || task.BranchId == query.BranchId.Value) &&
                (!query.DepartmentId.HasValue || task.DepartmentId == query.DepartmentId.Value) &&
                (!query.AssigneeUserId.HasValue || task.AssigneeUserId == query.AssigneeUserId.Value) &&
                (!query.Priority.HasValue || task.Priority == query.Priority.Value) &&
                (!query.TemplateId.HasValue || task.TaskTemplateId == query.TemplateId.Value) &&
                (!query.ScheduleId.HasValue || task.TaskScheduleId == query.ScheduleId.Value) &&
                (!query.DistributionId.HasValue || task.TaskDistributionId == query.DistributionId.Value) &&
                (!query.Overdue.HasValue ||
                    query.Overdue.Value ==
                    (task.DueAt < now &&
                        task.Status != OperationalTaskStatus.Completed &&
                        task.Status != OperationalTaskStatus.Cancelled)),
            query.PageQuery.ToDomain(),
            orderBy,
            cancellationToken);
        List<TaskDto> items = [];
        foreach (OperationalTask task in result.Items)
        {
            items.Add(await MapAsync(task, cancellationToken));
        }

        return new PagedResponse<TaskDto>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<TaskDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await MapAsync(await GetAuthorizedAsync(id, TaskAccess.View, cancellationToken), cancellationToken);

    public async Task<TaskDistributionResponse> CreateAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireSupervisorOrManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(
            organizationId,
            OpsManager.Domain.Constants.SubscriptionFeatureKeys.Tasks,
            cancellationToken);
        validator.ValidateAndThrow(request);
        await EnsureCanOperateDepartmentAsync(organizationId, request.DepartmentId, request.BranchId, cancellationToken);
        IReadOnlyList<ResolvedTaskAssignee> assignees = await assigneeResolver.ResolveAsync(
            organizationId,
            request.BranchId,
            request.DepartmentId,
            request.Assignment.Mode,
            request.Assignment.UserIds,
            cancellationToken);
        return await distributionCreator.CreateAsync(
            new TaskDistributionCreation(
                organizationId,
                request.BranchId,
                request.DepartmentId,
                null,
                null,
                null,
                request.Assignment.Mode,
                assignees,
                request.Title,
                request.Description,
                DateOnly.FromDateTime(request.ScheduledStartAt.UtcDateTime),
                request.ScheduledStartAt,
                request.DueAt,
                request.Priority,
                request.RequiresApproval,
                currentUser.UserId!.Value,
                request.Items.Select(ToCopyDefinition).ToArray()),
            cancellationToken);
    }

    public async Task<TaskDistributionResponse> CreateFromTemplateAsync(
        Guid templateId,
        CreateTaskFromTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireSupervisorOrManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(
            organizationId,
            OpsManager.Domain.Constants.SubscriptionFeatureKeys.Tasks,
            cancellationToken);
        TaskTemplate template = await unitOfWork.Repository<TaskTemplate>().GetByIdAsync(templateId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskTemplate));
        if (template.OrganizationId != organizationId)
        {
            throw new EntityNotFoundException(nameof(TaskTemplate));
        }

        if (!template.IsActive)
        {
            throw new ConflictException("The task template is inactive.", "inactive_template");
        }

        Guid departmentId = request.DepartmentId ?? template.DefaultDepartmentId
            ?? throw Validation(nameof(request.DepartmentId), "DepartmentId is required when the template has no default department.");
        Department department = await unitOfWork.Repository<Department>().GetByIdAsync(departmentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Department));
        Guid branchId = department.BranchId;
        await EnsureCanOperateDepartmentAsync(organizationId, departmentId, branchId, cancellationToken);
        CreateTaskValidator.ValidateAssignment(request.Assignment);
        IReadOnlyList<ResolvedTaskAssignee> assignees = await assigneeResolver.ResolveAsync(
            organizationId,
            branchId,
            departmentId,
            request.Assignment.Mode,
            request.Assignment.UserIds,
            cancellationToken);
        DateTimeOffset dueAt = request.DueAt ??
            request.ScheduledStartAt.AddMinutes(template.DefaultDurationMinutes ?? 60);
        if (dueAt <= request.ScheduledStartAt)
        {
            throw Validation(nameof(request.DueAt), "DueAt must be later than ScheduledStartAt.");
        }

        IReadOnlyList<TaskCopyItemDefinition> definitions;
        if (request.Items is not null)
        {
            SaveTaskTemplateValidator.ValidateItems(request.Items);
            PagedResult<TaskTemplateItem> tItems = await unitOfWork.Repository<TaskTemplateItem>().ListAsync(
                item => item.TaskTemplateId == templateId && item.IsActive,
                new PageRequest(1, PageRequest.MaximumPageSize),
                cancellationToken);
            var tMaxList = tItems.Items.OrderBy(i => i.SortOrder).ToList();
            definitions = request.Items.Select((req, idx) =>
            {
                int maxAtt = req.MaxAttachments > 0
                    ? req.MaxAttachments
                    : (idx < tMaxList.Count ? tMaxList[idx].MaxAttachments : 5);
                return new TaskCopyItemDefinition(
                    idx < tMaxList.Count ? tMaxList[idx].Id : null,
                    req.Title,
                    req.Description,
                    req.SortOrder,
                    req.IsRequired,
                    req.EvidenceMode,
                    req.ItemType,
                    req.Options,
                    req.MainBlockTitle,
                    req.SubBlockTitle,
                    maxAtt);
            }).ToArray();
        }
        else
        {
            PagedResult<TaskTemplateItem> templateItems = await unitOfWork.Repository<TaskTemplateItem>().ListAsync(
                item => item.TaskTemplateId == templateId && item.IsActive,
                new PageRequest(1, PageRequest.MaximumPageSize),
                cancellationToken);
            definitions = templateItems.Items
                .OrderBy(item => item.SortOrder)
                .Select(item => new TaskCopyItemDefinition(
                    item.Id,
                    item.Title,
                    item.Description,
                    item.SortOrder,
                    item.IsRequired,
                    item.EvidenceMode,
                    item.ItemType,
                    item.Options,
                    item.MainBlockTitle,
                    item.SubBlockTitle,
                    item.MaxAttachments))
                .ToArray();
        }

        return await distributionCreator.CreateAsync(
            new TaskDistributionCreation(
                organizationId,
                branchId,
                departmentId,
                template.Id,
                null,
                null,
                request.Assignment.Mode,
                assignees,
                template.Title,
                template.Description,
                DateOnly.FromDateTime(request.ScheduledStartAt.UtcDateTime),
                request.ScheduledStartAt,
                dueAt,
                request.Priority ?? template.DefaultPriority,
                request.RequiresApproval ?? template.RequiresApproval,
                currentUser.UserId!.Value,
                definitions),
            cancellationToken);
    }

    public async Task<TaskDto> UpdateAsync(
        Guid id,
        UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        OperationalTask task = await GetAuthorizedAsync(id, TaskAccess.Management, cancellationToken);
        EnsureMutable(task);
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 240 || request.DueAt <= request.ScheduledStartAt)
        {
            throw Validation(nameof(request), "Title is required and DueAt must be later than ScheduledStartAt.");
        }

        task.UpdateDetails(request.Title, request.Description, request.Priority, task.RequiresApproval);
        task.Reschedule(
            DateOnly.FromDateTime(request.ScheduledStartAt.Date),
            request.ScheduledStartAt,
            request.DueAt,
            task.TaskScheduleId.HasValue);
        unitOfWork.Repository<OperationalTask>().Update(task);
        await AuditAsync(task, "task.updated", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapAsync(task, cancellationToken);
    }

    public async Task<TaskDto> AssignAsync(
        Guid id,
        AssignTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireSupervisorOrManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        OperationalTask task = await GetAuthorizedAsync(id, TaskAccess.Management, cancellationToken);
        EnsureMutable(task);
        await ValidateAssigneeAsync(organizationId, task.BranchId, task.DepartmentId, request.AssigneeUserId, cancellationToken);
        bool duplicate = task.TaskDistributionId.HasValue &&
            await unitOfWork.Repository<OperationalTask>().AnyAsync(
                other => other.TaskDistributionId == task.TaskDistributionId &&
                    other.AssigneeUserId == request.AssigneeUserId &&
                    other.Id != task.Id,
                cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("The assignee already has a task in this distribution.", "duplicate_distribution_assignee");
        }

        Guid? previousAssignee = task.AssigneeUserId;
        task.Assign(request.AssigneeUserId);
        unitOfWork.Repository<OperationalTask>().Update(task);
        await unitOfWork.Repository<TaskAssignmentHistory>().AddAsync(
            new TaskAssignmentHistory(
                organizationId,
                task.Id,
                previousAssignee,
                request.AssigneeUserId,
                currentUser.UserId!.Value,
                clock.UtcNow),
            cancellationToken);
        await AuditAsync(task, "task.assigned", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifyAssigneeAsync(task, cancellationToken);
        return await MapAsync(task, cancellationToken);
    }

    public Task<TaskDto> StartAsync(Guid id, CancellationToken cancellationToken = default) =>
        ExecuteTransitionAsync(id, false, (task, actor, now, _) => task.Start(actor, now), null, cancellationToken, enforceExecutionWindow: true);

    public Task<TaskDto> BlockAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        ExecuteTransitionAsync(id, false, (task, actor, now, value) => task.Block(actor, now, value!), reason, cancellationToken);

    public Task<TaskDto> ResumeAsync(Guid id, CancellationToken cancellationToken = default) =>
        ExecuteTransitionAsync(id, false, (task, actor, now, _) => task.Resume(actor, now), null, cancellationToken);

    public Task<TaskDto> CompleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        ExecuteTransitionAsync(id, false, (task, actor, now, _) => task.Complete(actor, now, true), null, cancellationToken, requiresCompleteChecklist: true, enforceExecutionWindow: true);

    public Task<TaskDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default) =>
        ExecuteTransitionAsync(id, false, (task, actor, now, _) => task.SubmitForApproval(actor, now, true), null, cancellationToken, requiresCompleteChecklist: true, enforceExecutionWindow: true);

    public Task<TaskDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default) =>
        ExecuteTransitionAsync(id, true, (task, actor, now, _) => task.Approve(actor, now), null, cancellationToken);

    public Task<TaskDto> ReturnForCorrectionAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        ExecuteTransitionAsync(
            id,
            true,
            (task, actor, now, value) => task.ReturnForCorrection(actor, now, value!),
            reason,
            cancellationToken);

    public Task<TaskDto> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        ExecuteTransitionAsync(
            id,
            false,
            (task, actor, now, value) => task.Cancel(actor, now, value!),
            reason,
            cancellationToken,
            allowManagementOrOwner: true);

    private async Task<TaskDto> ExecuteTransitionAsync(
        Guid id,
        bool managementAction,
        Func<OperationalTask, Guid, DateTimeOffset, string?, TaskTransition> transition,
        string? reason,
        CancellationToken cancellationToken,
        bool requiresCompleteChecklist = false,
        bool allowManagementOrOwner = false,
        bool enforceExecutionWindow = false)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        TaskAccess access = managementAction
            ? TaskAccess.Management
            : allowManagementOrOwner && currentUser.OrganizationRole != OrganizationRole.Employee
                ? TaskAccess.Management
                : TaskAccess.Execution;
        OperationalTask task = await GetAuthorizedAsync(id, access, cancellationToken);
        if (managementAction && currentUser.OrganizationRole == OrganizationRole.Employee)
        {
            throw new ForbiddenAccessException("Approval actions require Supervisor or Manager access.");
        }

        if (enforceExecutionWindow)
        {
            DateTimeOffset now = clock.UtcNow;
            if (now < task.ScheduledStartAt)
            {
                throw new TaskExecutionWindowException(
                    $"This task is not available until {task.ScheduledStartAt:u}.",
                    "task_not_started_yet",
                    task.ScheduledStartAt,
                    task.DueAt);
            }

            if (now > task.DueAt)
            {
                throw new TaskExecutionWindowException(
                    "The execution deadline for this task has passed.",
                    "task_execution_window_expired",
                    task.ScheduledStartAt,
                    task.DueAt);
            }
        }

        if (requiresCompleteChecklist)
        {
            await EnsureChecklistCompleteAsync(task.Id, cancellationToken);
        }

        TaskTransition result = transition(task, currentUser.UserId!.Value, clock.UtcNow, reason);
        unitOfWork.Repository<OperationalTask>().Update(task);
        TaskStatusHistory history = TaskStatusHistory.FromTransition(organizationId, task.Id, result);
        await unitOfWork.Repository<TaskStatusHistory>().AddAsync(history, cancellationToken);
        await AuditAsync(task, $"task.{task.Status.ToString().ToLowerInvariant()}", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (task.Status == OperationalTaskStatus.Returned && task.AssigneeUserId.HasValue)
        {
            await TryNotifyAsync(
                organizationId,
                task.AssigneeUserId.Value,
                NotificationType.TaskAssigned,
                "Task returned",
                task.Title,
                task,
                cancellationToken);
        }

        return await MapAsync(task, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireSupervisorOrManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        OperationalTask task = await GetAuthorizedAsync(id, TaskAccess.Management, cancellationToken);
        unitOfWork.Repository<OperationalTask>().Remove(task);
        await AuditAsync(task, "task.deleted", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<TaskDistributionResponse> CloneAsync(
        Guid id,
        CloneTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        OperationalTask source = await GetAuthorizedAsync(id, TaskAccess.Management, cancellationToken);
        CreateTaskValidator.ValidateAssignment(request.Assignment);
        IReadOnlyList<ResolvedTaskAssignee> assignees = await assigneeResolver.ResolveAsync(
            organizationId,
            source.BranchId,
            source.DepartmentId,
            request.Assignment.Mode,
            request.Assignment.UserIds,
            cancellationToken);
        TimeSpan duration = source.DueAt - source.ScheduledStartAt;
        DateTimeOffset start = clock.UtcNow;
        PagedResult<TaskItem> sourceItems = await GetItemsAsync(source.Id, cancellationToken);
        return await distributionCreator.CreateAsync(
            new TaskDistributionCreation(
                organizationId,
                source.BranchId,
                source.DepartmentId,
                source.TaskTemplateId,
                null,
                source.Id,
                request.Assignment.Mode,
                assignees,
                $"{source.Title} (Copy)",
                source.Description,
                DateOnly.FromDateTime(start.UtcDateTime),
                start,
                start.Add(duration),
                source.Priority,
                source.RequiresApproval,
                currentUser.UserId!.Value,
                sourceItems.Items.Select(item => new TaskCopyItemDefinition(
                    item.TemplateItemId,
                    item.Title,
                    item.Description,
                    item.SortOrder,
                    item.IsRequired,
                    item.EvidenceMode,
                    item.ItemType,
                    item.Options,
                    item.MainBlockTitle,
                    item.SubBlockTitle,
                    item.MaxAttachments)).ToArray()),
            cancellationToken);
    }

    public async Task<TaskItemDto> UpdateItemAsync(
        Guid taskId,
        Guid itemId,
        UpdateTaskItemRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        OperationalTask task = await GetAuthorizedAsync(taskId, TaskAccess.Execution, cancellationToken);
        EnsureMutable(task);
        TaskItem item = await unitOfWork.Repository<TaskItem>()
            .FirstOrDefaultAsync(entity => entity.Id == itemId && entity.TaskId == taskId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskItem));
        int attachmentCount = await unitOfWork.Repository<TaskAttachment>()
            .CountAsync(attachment => attachment.TaskId == taskId && attachment.TaskItemId == itemId, cancellationToken);
        switch (request.Status)
        {
            case TaskItemStatus.Completed:
                item.Complete(currentUser.UserId!.Value, clock.UtcNow, attachmentCount > 0);
                break;
            case TaskItemStatus.Pending:
                item.Reset();
                break;
            case TaskItemStatus.Skipped:
                item.Skip();
                break;
            default:
                throw Validation(nameof(request.Status), "Unsupported checklist status.");
        }

        item.SetNote(request.Note);
        item.SetValue(request.Value);
        unitOfWork.Repository<TaskItem>().Update(item);
        await AuditAsync(task, "task-item.updated", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(item, attachmentCount);
    }

    public async Task<StoredFile> AddAttachmentAsync(
        Guid taskId,
        Guid itemId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        OperationalTask task = await GetAuthorizedAsync(taskId, TaskAccess.Execution, cancellationToken);
        EnsureMutable(task);
        TaskItem targetItem = await unitOfWork.Repository<TaskItem>()
            .FirstOrDefaultAsync(item => item.Id == itemId && item.TaskId == taskId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskItem));

        int maxAllowed = targetItem.MaxAttachments;
        if (targetItem.TemplateItemId.HasValue)
        {
            TaskTemplateItem? templateItem = await unitOfWork.Repository<TaskTemplateItem>()
                .GetByIdAsync(targetItem.TemplateItemId.Value, cancellationToken);
            if (templateItem != null)
            {
                maxAllowed = templateItem.MaxAttachments;
            }
        }

        int existingCount = await unitOfWork.Repository<TaskAttachment>()
            .CountAsync(attachment => attachment.TaskId == taskId && attachment.TaskItemId == itemId, cancellationToken);
        if (existingCount >= maxAllowed)
        {
            throw new ConflictException($"Maximum attachment limit ({maxAllowed}) reached for this item.", "max_attachments_exceeded");
        }
        StoredFile file = await fileStorage.SaveAsync(content, fileName, contentType, cancellationToken);
        TaskAttachment attachment = new(
            organizationId,
            taskId,
            itemId,
            currentUser.UserId!.Value,
            file.Url,
            file.ContentType,
            AttachmentType.Evidence);
        await unitOfWork.Repository<TaskAttachment>().AddAsync(attachment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return file;
    }

    public async Task DeleteAttachmentAsync(
        Guid taskId,
        Guid itemId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        OperationalTask task = await GetAuthorizedAsync(taskId, TaskAccess.Execution, cancellationToken);
        EnsureMutable(task);
        TaskAttachment attachment = await unitOfWork.Repository<TaskAttachment>().FirstOrDefaultAsync(
            item => item.Id == attachmentId && item.TaskId == taskId && item.TaskItemId == itemId,
            cancellationToken)
            ?? throw new EntityNotFoundException(nameof(TaskAttachment));
        unitOfWork.Repository<TaskAttachment>().DeletePermanently(attachment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await fileStorage.DeleteAsync(attachment.FileUrl, cancellationToken);
    }

    private async Task<OperationalTask> GetAuthorizedAsync(
        Guid id,
        TaskAccess access,
        CancellationToken cancellationToken)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        OperationalTask task = await unitOfWork.Repository<OperationalTask>().GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(OperationalTask));
        if (task.OrganizationId != organizationId)
        {
            throw new EntityNotFoundException(nameof(OperationalTask));
        }

        if (access == TaskAccess.Execution)
        {
            if (task.AssigneeUserId != currentUser.UserId)
            {
                throw new EntityNotFoundException(nameof(OperationalTask));
            }

            return task;
        }

        if (currentUser.OrganizationRole == OrganizationRole.Employee)
        {
            if (access == TaskAccess.Management || task.AssigneeUserId != currentUser.UserId)
            {
                throw new EntityNotFoundException(nameof(OperationalTask));
            }

            return task;
        }

        IReadOnlyList<Guid>? allowed = await GetAllowedDepartmentIdsAsync(organizationId, cancellationToken);
        if (allowed is not null && !allowed.Contains(task.DepartmentId))
        {
            throw new EntityNotFoundException(nameof(OperationalTask));
        }

        return task;
    }

    private async Task EnsureCanOperateDepartmentAsync(
        Guid organizationId,
        Guid departmentId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        Department department = await unitOfWork.Repository<Department>().GetByIdAsync(departmentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Department));
        if (department.OrganizationId != organizationId)
        {
            throw new EntityNotFoundException(nameof(Department));
        }

        if (!department.IsActive || department.BranchId != branchId)
        {
            throw Validation(nameof(departmentId), "The department must be active and belong to the selected branch.");
        }

        IReadOnlyList<Guid>? allowed = await GetAllowedDepartmentIdsAsync(organizationId, cancellationToken);
        if (allowed is not null && !allowed.Contains(departmentId))
        {
            throw new ForbiddenAccessException("The selected department is outside your authorization scope.");
        }

    }

    private async Task ValidateAssigneeAsync(
        Guid organizationId,
        Guid branchId,
        Guid departmentId,
        Guid assigneeId,
        CancellationToken cancellationToken)
    {
        _ = await assigneeResolver.ResolveAsync(
            organizationId,
            branchId,
            departmentId,
            TaskAssignmentMode.SingleUser,
            [assigneeId],
            cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>?> GetAllowedDepartmentIdsAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
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
            department => department.OrganizationId == organizationId && department.SupervisorUserId == userId && department.IsActive,
            department => department.Id,
            cancellationToken);
        return memberships.Concat(supervised).Distinct().ToArray();
    }

    private async Task EnsureChecklistCompleteAsync(Guid taskId, CancellationToken cancellationToken)
    {
        bool incomplete = await unitOfWork.Repository<TaskItem>()
            .AnyAsync(item => item.TaskId == taskId && item.IsRequired && item.Status != TaskItemStatus.Completed, cancellationToken);
        if (incomplete)
        {
            throw new ConflictException("All required checklist items must be completed.", "required_items_incomplete");
        }
    }

    private async Task RecordCreationAsync(OperationalTask task, CancellationToken cancellationToken)
    {
        TaskStatusHistory history = TaskStatusHistory.Created(
            task.OrganizationId,
            task.Id,
            currentUser.UserId!.Value,
            clock.UtcNow);
        await unitOfWork.Repository<TaskStatusHistory>().AddAsync(history, cancellationToken);
        await AuditAsync(task, "task.created", cancellationToken);
    }

    private Task AuditAsync(OperationalTask task, string action, CancellationToken cancellationToken) =>
        auditService.RecordTenantAsync(
            task.OrganizationId,
            action,
            nameof(OperationalTask),
            task.Id,
            cancellationToken: cancellationToken);

    private async Task NotifyAssigneeAsync(OperationalTask task, CancellationToken cancellationToken)
    {
        if (task.AssigneeUserId.HasValue)
        {
            await TryNotifyAsync(
                task.OrganizationId,
                task.AssigneeUserId.Value,
                NotificationType.TaskAssigned,
                "Task assigned",
                task.Title,
                task,
                cancellationToken);
        }
    }

    private async Task TryNotifyAsync(
        Guid organizationId,
        Guid userId,
        NotificationType type,
        string title,
        string body,
        OperationalTask task,
        CancellationToken cancellationToken)
    {
        try
        {
            await notifications.CreateAsync(
                organizationId,
                userId,
                type,
                title,
                body,
                relatedEntityType: nameof(OperationalTask),
                relatedEntityId: task.Id,
                cancellationToken: cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Notifications are best-effort after the primary transaction in the MVP.
        }
    }

    private async Task<PagedResult<TaskItem>> GetItemsAsync(Guid taskId, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<TaskItem>().ListAsync(
            item => item.TaskId == taskId,
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);

    private async Task<TaskDto> MapAsync(OperationalTask task, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.UtcNow;
        PagedResult<TaskItem> items = await GetItemsAsync(task.Id, cancellationToken);
        User? assignee = task.AssigneeUserId.HasValue
            ? await unitOfWork.Repository<User>().GetByIdAsync(task.AssigneeUserId.Value, cancellationToken)
            : null;
        Department? department = await unitOfWork.Repository<Department>().GetByIdAsync(task.DepartmentId, cancellationToken);
        PagedResult<TaskAttachment> attachmentsResult = await unitOfWork.Repository<TaskAttachment>().ListAsync(
            attachment => attachment.TaskId == task.Id,
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);
        IReadOnlyList<TaskAttachment> attachments = attachmentsResult.Items;
        TaskExecutionWindowState windowState = task.GetExecutionWindowState(now);
        bool canStart = task.CanStartInWindow(now);
        bool canComplete = task.CanCompleteInWindow(now);
        Dictionary<Guid, int> templateMaxMap = new();
        if (task.TaskTemplateId.HasValue)
        {
            PagedResult<TaskTemplateItem> tItems = await unitOfWork.Repository<TaskTemplateItem>().ListAsync(
                ti => ti.TaskTemplateId == task.TaskTemplateId.Value && ti.IsActive,
                new PageRequest(1, PageRequest.MaximumPageSize),
                cancellationToken);
            foreach (TaskTemplateItem ti in tItems.Items)
            {
                templateMaxMap[ti.Id] = ti.MaxAttachments;
            }
        }

        return new TaskDto(
            task.Id,
            task.BranchId,
            task.DepartmentId,
            task.TaskDistributionId,
            task.AssigneeUserId,
            assignee?.FullName,
            task.TaskTemplateId,
            task.TaskScheduleId,
            task.ParentTaskId,
            task.Title,
            task.Description,
            task.OccurrenceDate,
            task.ScheduledStartAt,
            task.DueAt,
            task.Priority,
            task.Status,
            task.RequiresApproval,
            task.IsOverdue(now),
            task.StartedAt,
            task.SubmittedForApprovalAt,
            task.CompletedAt,
            task.ApprovedAt,
            task.ApprovedBy,
            task.BlockedReason,
            items.Items
                .OrderBy(item => item.SortOrder)
                .Select(item =>
                {
                    var itemAtts = attachments
                        .Where(a => a.TaskItemId == item.Id)
                        .Select(a => new TaskAttachmentDto(a.Id, fileStorage.ResolveUrl(a.FileUrl), a.FileType))
                        .ToList();
                    int maxAtt = (item.TemplateItemId.HasValue && templateMaxMap.TryGetValue(item.TemplateItemId.Value, out int tm))
                        ? tm
                        : item.MaxAttachments;
                    return Map(item, itemAtts.Count, itemAtts, maxAtt);
                })
                .ToArray(),
            windowState,
            canStart,
            canComplete,
            department?.Name);
    }

    private static TaskCopyItemDefinition ToCopyDefinition(ChecklistDefinitionRequest request) =>
        new(
            null,
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

    private static TaskItemDto Map(TaskItem item, int attachmentCount, IReadOnlyList<TaskAttachmentDto>? attachments = null, int? overrideMaxAttachments = null) =>
        new(
            item.Id,
            item.Title,
            item.Description,
            item.SortOrder,
            item.IsRequired,
            item.EvidenceMode,
            item.Status,
            item.CompletedBy,
            item.CompletedAt,
            item.Note,
            attachmentCount,
            item.ItemType,
            item.Options,
            item.MainBlockTitle,
            item.SubBlockTitle,
            item.Value,
            attachments,
            overrideMaxAttachments ?? item.MaxAttachments);

    private static void EnsureMutable(OperationalTask task)
    {
        if (task.Status is OperationalTaskStatus.Completed or OperationalTaskStatus.Cancelled)
        {
            throw new ConflictException("A completed, approved, or cancelled task is immutable.", "task_immutable");
        }

        if (task.Status is OperationalTaskStatus.NotStarted)
        {
            throw new ConflictException("Task items cannot be modified before the task is started.", "task_not_started");
        }
    }

    private static RequestValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private enum TaskAccess
    {
        View,
        Execution,
        Management,
    }
}
