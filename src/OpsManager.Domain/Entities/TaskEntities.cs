using OpsManager.Domain.Common;
using OpsManager.Domain.Enums;
using TaskStatus = OpsManager.Domain.Enums.TaskStatus;

namespace OpsManager.Domain.Entities;

public sealed class TaskTemplate : TenantSoftDeletableEntity
{
    private TaskTemplate() { }

    public TaskTemplate(Guid organizationId, Guid defaultDepartmentId, string title, Guid createdBy)
    {
        OrganizationId = organizationId;
        DefaultDepartmentId = defaultDepartmentId;
        Title = Guard.Required(title, nameof(title), 240);
        CreatedBy = createdBy;
    }

    public Guid? BranchId { get; set; }
    public Guid DefaultDepartmentId { get; set; }
    public Guid? DefaultAssigneeUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority DefaultPriority { get; set; } = TaskPriority.Normal;
    public int? DefaultDurationMinutes { get; set; }
    public bool RequiresApproval { get; set; }
    public Guid CreatedBy { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class TaskTemplateItem : TenantAuditableEntity
{
    private TaskTemplateItem() { }

    public TaskTemplateItem(Guid organizationId, Guid taskTemplateId, string title, int sortOrder)
    {
        OrganizationId = organizationId;
        TaskTemplateId = taskTemplateId;
        Title = Guard.Required(title, nameof(title), 240);
        SortOrder = sortOrder;
    }

    public Guid TaskTemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
    public EvidenceMode EvidenceMode { get; set; }
}

public sealed class TaskTemplateItemAttachment : TenantAuditableEntity
{
    public Guid TaskTemplateItemId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public Guid UploadedBy { get; set; }
}

public sealed class TaskSchedule : TenantAuditableEntity
{
    private TaskSchedule() { }

    public TaskSchedule(
        Guid organizationId,
        Guid taskTemplateId,
        Guid branchId,
        Guid departmentId,
        RecurrenceType recurrenceType,
        int recurrenceInterval,
        DateOnly startDate,
        TimeOnly startTime,
        TimeOnly dueTime,
        IEnumerable<int>? weekdays = null,
        int? monthDay = null,
        DateOnly? endDate = null)
    {
        OrganizationId = organizationId;
        TaskTemplateId = taskTemplateId;
        BranchId = branchId;
        DepartmentId = departmentId;
        RecurrenceType = recurrenceType;
        RecurrenceInterval = recurrenceInterval;
        Weekdays = weekdays?.Distinct().Order().ToArray() ?? [];
        MonthDay = monthDay;
        StartDate = startDate;
        EndDate = endDate;
        StartTime = startTime;
        DueTime = dueTime;
        ValidateRecurrence();
    }

    public Guid TaskTemplateId { get; set; }
    public Guid BranchId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public RecurrenceType RecurrenceType { get; set; }
    public int RecurrenceInterval { get; set; }
    public int[] Weekdays { get; set; } = [];
    public int? MonthDay { get; set; }
    public string? RecurrenceRule { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly DueTime { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }

    public void ValidateRecurrence()
    {
        if (RecurrenceInterval <= 0)
        {
            throw new DomainInvariantException("Recurrence interval must be positive.");
        }

        if (EndDate < StartDate)
        {
            throw new DomainInvariantException("Schedule end date cannot precede its start date.");
        }

        if (DueTime <= StartTime)
        {
            throw new DomainInvariantException("Schedule due time must be later than its start time.");
        }

        if (RecurrenceType == RecurrenceType.Weekly &&
            (Weekdays.Length == 0 || Weekdays.Any(day => day is < 0 or > 6)))
        {
            throw new DomainInvariantException("Weekly recurrence requires weekdays between 0 and 6.");
        }

        if (RecurrenceType == RecurrenceType.Monthly && MonthDay is not (>= 1 and <= 31))
        {
            throw new DomainInvariantException("Monthly recurrence requires a month day between 1 and 31.");
        }
    }
}

public sealed class Task : TenantSoftDeletableEntity
{
    private static readonly Dictionary<TaskStatus, IReadOnlySet<TaskStatus>> AllowedTransitions =
        new Dictionary<TaskStatus, IReadOnlySet<TaskStatus>>
        {
            [TaskStatus.Pending] = new HashSet<TaskStatus> { TaskStatus.InProgress, TaskStatus.Cancelled },
            [TaskStatus.InProgress] = new HashSet<TaskStatus> { TaskStatus.Blocked, TaskStatus.Completed, TaskStatus.AwaitingApproval, TaskStatus.Cancelled },
            [TaskStatus.Blocked] = new HashSet<TaskStatus> { TaskStatus.InProgress, TaskStatus.Cancelled },
            [TaskStatus.AwaitingApproval] = new HashSet<TaskStatus> { TaskStatus.Approved, TaskStatus.Rejected },
            [TaskStatus.Rejected] = new HashSet<TaskStatus> { TaskStatus.InProgress, TaskStatus.Cancelled },
        };

    private Task() { }

    public Task(
        Guid organizationId,
        Guid branchId,
        Guid departmentId,
        string title,
        DateOnly occurrenceDate,
        DateTimeOffset scheduledStartAt,
        DateTimeOffset dueAt,
        Guid createdBy)
    {
        if (dueAt <= scheduledStartAt)
        {
            throw new DomainInvariantException("Task due time must be later than its scheduled start time.");
        }

        OrganizationId = organizationId;
        BranchId = branchId;
        DepartmentId = departmentId;
        Title = Guard.Required(title, nameof(title), 240);
        OccurrenceDate = occurrenceDate;
        ScheduledStartAt = scheduledStartAt;
        DueAt = dueAt;
        CreatedBy = createdBy;
    }

    public Guid BranchId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public Guid? TaskTemplateId { get; set; }
    public Guid? TaskScheduleId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly OccurrenceDate { get; set; }
    public DateTimeOffset ScheduledStartAt { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public TaskStatus Status { get; private set; } = TaskStatus.Pending;
    public bool RequiresApproval { get; set; }
    public bool IsScheduleOverride { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public string? BlockedReason { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }

    public void TransitionTo(TaskStatus newStatus, Guid actorId, DateTimeOffset occurredAt, string? reason = null)
    {
        if (!AllowedTransitions.TryGetValue(Status, out IReadOnlySet<TaskStatus>? targets) || !targets.Contains(newStatus))
        {
            throw new InvalidStateTransitionException(nameof(Task), Status.ToString(), newStatus.ToString());
        }

        if (newStatus == TaskStatus.Completed && RequiresApproval)
        {
            throw new DomainInvariantException("A task requiring approval must transition to AwaitingApproval.");
        }

        if (newStatus == TaskStatus.AwaitingApproval && !RequiresApproval)
        {
            throw new DomainInvariantException("A task without approval cannot transition to AwaitingApproval.");
        }

        Status = newStatus;
        switch (newStatus)
        {
            case TaskStatus.InProgress:
                StartedAt ??= occurredAt;
                BlockedReason = null;
                break;
            case TaskStatus.Blocked:
                BlockedReason = Guard.Required(reason, nameof(reason), 1000);
                break;
            case TaskStatus.Completed:
            case TaskStatus.AwaitingApproval:
                CompletedAt = occurredAt;
                break;
            case TaskStatus.Approved:
                ApprovedAt = occurredAt;
                ApprovedBy = actorId;
                break;
            case TaskStatus.Cancelled:
                CancelledAt = occurredAt;
                CancelledBy = actorId;
                break;
        }
    }
}

public sealed class TaskItem : TenantAuditableEntity
{
    public Guid TaskId { get; set; }
    public Guid? TemplateItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
    public EvidenceMode EvidenceMode { get; set; }
    public TaskItemStatus Status { get; private set; }
    public Guid? CompletedBy { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? Note { get; set; }

    public void Complete(Guid completedBy, DateTimeOffset completedAt, bool hasEvidenceAttachment)
    {
        if (IsRequired && EvidenceMode == EvidenceMode.Required && !hasEvidenceAttachment)
        {
            throw new DomainInvariantException("A required item with required evidence needs an attachment before completion.");
        }

        Status = TaskItemStatus.Completed;
        CompletedBy = completedBy;
        CompletedAt = completedAt;
    }
}

public sealed class TaskAttachment : TenantAuditableEntity
{
    public Guid TaskId { get; set; }
    public Guid? TaskItemId { get; set; }
    public Guid UploadedBy { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public AttachmentType AttachmentType { get; set; }
    public string? Caption { get; set; }
}

public sealed class TaskStatusHistory : TenantAuditableEntity
{
    public Guid TaskId { get; set; }
    public TaskStatus? OldStatus { get; set; }
    public TaskStatus NewStatus { get; set; }
    public Guid ChangedBy { get; set; }
    public string? Reason { get; set; }
}
