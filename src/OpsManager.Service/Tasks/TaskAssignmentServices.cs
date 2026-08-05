using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Tasks.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Tasks;

public sealed record ResolvedTaskAssignee(Guid UserId, string FullName);

public interface ITaskAssigneeResolver
{
    Task<IReadOnlyList<ResolvedTaskAssignee>> ResolveAsync(
        Guid organizationId,
        Guid branchId,
        Guid departmentId,
        TaskAssignmentMode mode,
        IReadOnlyCollection<Guid> requestedUserIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResolvedTaskAssignee>> ResolveScheduledAsync(
        Guid organizationId,
        Guid branchId,
        Guid departmentId,
        TaskAssignmentMode mode,
        IReadOnlyCollection<Guid> configuredUserIds,
        CancellationToken cancellationToken = default);
}

public sealed class TaskAssigneeResolver(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser) : ITaskAssigneeResolver
{
    public Task<IReadOnlyList<ResolvedTaskAssignee>> ResolveAsync(
        Guid organizationId,
        Guid branchId,
        Guid departmentId,
        TaskAssignmentMode mode,
        IReadOnlyCollection<Guid> requestedUserIds,
        CancellationToken cancellationToken = default) =>
        ResolveCoreAsync(
            organizationId,
            branchId,
            departmentId,
            mode,
            requestedUserIds,
            requireEveryRequestedUser: true,
            cancellationToken);

    public Task<IReadOnlyList<ResolvedTaskAssignee>> ResolveScheduledAsync(
        Guid organizationId,
        Guid branchId,
        Guid departmentId,
        TaskAssignmentMode mode,
        IReadOnlyCollection<Guid> configuredUserIds,
        CancellationToken cancellationToken = default) =>
        ResolveCoreAsync(
            organizationId,
            branchId,
            departmentId,
            mode,
            configuredUserIds,
            requireEveryRequestedUser: false,
            cancellationToken);

    private async Task<IReadOnlyList<ResolvedTaskAssignee>> ResolveCoreAsync(
        Guid organizationId,
        Guid branchId,
        Guid departmentId,
        TaskAssignmentMode mode,
        IReadOnlyCollection<Guid> requestedUserIds,
        bool requireEveryRequestedUser,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(mode))
        {
            throw Validation(nameof(mode), "Assignment mode is not supported.");
        }

        Guid[] requested = requestedUserIds.ToArray();
        if (requested.Any(userId => userId == Guid.Empty) || requested.Distinct().Count() != requested.Length)
        {
            throw Validation(nameof(requestedUserIds), "Assignee identifiers must be non-empty and unique.");
        }

        if (requireEveryRequestedUser)
        {
            CreateTaskValidator.ValidateAssignment(new TaskAssignmentRequest(mode, requested));
        }
        else if (mode == TaskAssignmentMode.AllDepartmentMembers && requested.Length != 0)
        {
            throw Validation(nameof(requestedUserIds), "AllDepartmentMembers cannot have fixed assignees.");
        }

        Department department = await unitOfWork.Repository<Department>().GetByIdAsync(departmentId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Department));
        if (department.OrganizationId != organizationId ||
            department.BranchId != branchId ||
            !department.IsActive)
        {
            throw Validation(nameof(departmentId), "The department must be active and belong to the selected branch.");
        }

        bool isSupervisor = currentUser.IsAuthenticated && currentUser.OrganizationRole == OrganizationRole.Supervisor;

        IReadOnlyList<Guid> departmentUsers = await unitOfWork.Repository<UserDepartment>().ProjectAsync(
            relation => relation.OrganizationId == organizationId &&
                relation.DepartmentId == departmentId &&
                relation.LeftAt == null,
            relation => relation.UserId,
            cancellationToken);

        HashSet<Guid> departmentUserSet = departmentUsers.ToHashSet();
        if (department.SupervisorUserId.HasValue)
        {
            departmentUserSet.Add(department.SupervisorUserId.Value);
        }

        IReadOnlyList<Guid> employeeMembers = await unitOfWork.Repository<OrganizationMember>().ProjectAsync(
            member => member.OrganizationId == organizationId &&
                member.IsActive &&
                member.LeftAt == null &&
                (
                    (member.Role == OrganizationRole.Employee && departmentUserSet.Contains(member.UserId)) ||
                    (!isSupervisor && member.Role == OrganizationRole.Supervisor)
                ),
            member => member.UserId,
            cancellationToken);
        HashSet<Guid> eligibleIds = employeeMembers.ToHashSet();
        if (mode != TaskAssignmentMode.AllDepartmentMembers)
        {
            eligibleIds.IntersectWith(requested);
        }

        IReadOnlyList<ResolvedTaskAssignee> activeUsers = await unitOfWork.Repository<User>().ProjectAsync(
            user => eligibleIds.Contains(user.Id) && user.AccountStatus == UserAccountStatus.Active,
            user => new ResolvedTaskAssignee(user.Id, user.FullName),
            cancellationToken);
        ResolvedTaskAssignee[] resolved = activeUsers
            .DistinctBy(user => user.UserId)
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.UserId)
            .ToArray();
        if (requireEveryRequestedUser &&
            mode != TaskAssignmentMode.AllDepartmentMembers &&
            resolved.Length != requested.Length)
        {
            string roleDescription = isSupervisor ? "employee" : "employee or supervisor";
            throw Validation(
                nameof(requestedUserIds),
                $"Every assignee must be an active {roleDescription} assigned to the selected department.");
        }

        if (resolved.Length == 0)
        {
            string roleDescription = isSupervisor ? "employees" : "employees or supervisors";
            throw new ConflictException(
                $"No active department {roleDescription} are eligible for this task.",
                "no_eligible_task_assignees");
        }

        return resolved;
    }

    private static RequestValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

public sealed record TaskCopyItemDefinition(
    Guid? TemplateItemId,
    string Title,
    string? Description,
    int SortOrder,
    bool IsRequired,
    EvidenceMode EvidenceMode,
    TaskItemType ItemType = TaskItemType.SingleLineText,
    string? Options = null,
    string? MainBlockTitle = null,
    string? SubBlockTitle = null);

public sealed record TaskDistributionCreation(
    Guid OrganizationId,
    Guid BranchId,
    Guid DepartmentId,
    Guid? TaskTemplateId,
    Guid? TaskScheduleId,
    Guid? ParentTaskId,
    TaskAssignmentMode AssignmentMode,
    IReadOnlyList<ResolvedTaskAssignee> Assignees,
    string Title,
    string? Description,
    DateOnly OccurrenceDate,
    DateTimeOffset ScheduledStartAt,
    DateTimeOffset DueAt,
    TaskPriority Priority,
    bool RequiresApproval,
    Guid CreatedBy,
    IReadOnlyList<TaskCopyItemDefinition> Items);

public interface ITaskDistributionCreator
{
    Task<TaskDistributionResponse> CreateAsync(
        TaskDistributionCreation creation,
        CancellationToken cancellationToken = default);
}

public sealed class TaskDistributionCreator(
    IUnitOfWork unitOfWork,
    INotificationService notifications,
    IAuditService auditService,
    IClock clock) : ITaskDistributionCreator
{
    public async Task<TaskDistributionResponse> CreateAsync(
        TaskDistributionCreation creation,
        CancellationToken cancellationToken = default)
    {
        if (creation.Assignees.Count == 0)
        {
            throw new ConflictException("A distribution requires at least one assignee.", "empty_task_distribution");
        }
        TaskDistributionResponse response = await unitOfWork.ExecuteWithStrategyAsync(async () =>
        {
            await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                TaskDistribution distribution = new(
                    creation.OrganizationId,
                    creation.BranchId,
                    creation.DepartmentId,
                    creation.AssignmentMode,
                    creation.OccurrenceDate,
                    creation.ScheduledStartAt,
                    creation.DueAt,
                    creation.CreatedBy,
                    creation.TaskTemplateId,
                    creation.TaskScheduleId);
                await unitOfWork.Repository<TaskDistribution>().AddAsync(distribution, cancellationToken);

                List<DistributedTaskResponse> results = [];
                foreach (ResolvedTaskAssignee assignee in creation.Assignees)
                {
                    OperationalTask task = OperationalTask.CreateAssignedCopy(
                        creation.OrganizationId,
                        distribution.Id,
                        creation.BranchId,
                        creation.DepartmentId,
                        assignee.UserId,
                        creation.TaskTemplateId,
                        creation.TaskScheduleId,
                        creation.ParentTaskId,
                        creation.Title,
                        creation.Description,
                        creation.OccurrenceDate,
                        creation.ScheduledStartAt,
                        creation.DueAt,
                        creation.Priority,
                        creation.RequiresApproval,
                        creation.CreatedBy);
                    await unitOfWork.Repository<OperationalTask>().AddAsync(task, cancellationToken);
                    await unitOfWork.Repository<TaskItem>().AddRangeAsync(
                        creation.Items.Select(item => new TaskItem(
                            creation.OrganizationId,
                            task.Id,
                            item.Title,
                            item.SortOrder,
                            item.IsRequired,
                            item.EvidenceMode,
                            item.TemplateItemId,
                            description: item.Description,
                            itemType: item.ItemType,
                            options: item.Options,
                            mainBlockTitle: item.MainBlockTitle,
                            subBlockTitle: item.SubBlockTitle)),
                        cancellationToken);
                    await unitOfWork.Repository<TaskStatusHistory>().AddAsync(
                        TaskStatusHistory.Created(
                            creation.OrganizationId,
                            task.Id,
                            creation.CreatedBy,
                            clock.UtcNow),
                        cancellationToken);
                    await notifications.CreateAsync(
                        creation.OrganizationId,
                        assignee.UserId,
                        NotificationType.TaskAssigned,
                        "Task assigned",
                        task.Title,
                        relatedEntityType: nameof(OperationalTask),
                        relatedEntityId: task.Id,
                        cancellationToken: cancellationToken);
                    results.Add(new DistributedTaskResponse(
                        task.Id,
                        assignee.UserId,
                        assignee.FullName,
                        task.Status));
                }

                await auditService.RecordTenantAsync(
                    creation.OrganizationId,
                    "task-distribution.created",
                    nameof(TaskDistribution),
                    distribution.Id,
                    newValues: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["assignmentMode"] = creation.AssignmentMode.ToString(),
                        ["taskCount"] = results.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                    cancellationToken: cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new TaskDistributionResponse(
                    distribution.Id,
                    distribution.AssignmentMode,
                    results.Count,
                    results);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
         

        });
        return response;
        
    }
}
