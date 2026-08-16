using OpsManager.Domain.Common;
using OpsManager.Domain.Enums;

namespace OpsManager.Domain.Entities;

public sealed record TaskTransition(
    OperationalTaskStatus OldStatus,
    OperationalTaskStatus NewStatus,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string? Reason);

public sealed class TaskTemplate : TenantSoftDeletableEntity
{
    private TaskTemplate() { }

    public TaskTemplate(Guid organizationId, Guid? defaultDepartmentId, string title, Guid createdBy)
    {
        OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId));

        if (defaultDepartmentId == Guid.Empty)
        {
            throw new DomainInvariantException(
                "Default department identifier cannot be empty.");
        }
        DefaultDepartmentId = defaultDepartmentId;
        Title = Guard.Required(title, nameof(title), 240);
        CreatedBy = Guard.NotEmpty(createdBy, nameof(createdBy));
    }

    public Guid? DefaultDepartmentId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskPriority DefaultPriority { get; private set; } = TaskPriority.Normal;
    public int? DefaultDurationMinutes { get; private set; }
    public bool RequiresApproval { get; private set; }
    public Guid CreatedBy { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(
        Guid? defaultDepartmentId,
        string title,
        string? description,
        TaskPriority defaultPriority,
        int? defaultDurationMinutes,
        bool requiresApproval)
    {
        if (defaultDepartmentId == Guid.Empty)
        {
            throw new DomainInvariantException("Optional identifiers cannot be empty.");
        }

        if (defaultDurationMinutes.HasValue)
        {
            Guard.Positive(defaultDurationMinutes.Value, nameof(defaultDurationMinutes));
        }

        DefaultDepartmentId = defaultDepartmentId;
        Title = Guard.Required(title, nameof(title), 240);
        Description = Guard.Optional(description, 4000);
        DefaultPriority = defaultPriority;
        DefaultDurationMinutes = defaultDurationMinutes;
        RequiresApproval = requiresApproval;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}

public sealed class TaskTemplateItem : TenantAuditableEntity
{
    private TaskTemplateItem() { }

    public TaskTemplateItem(
        Guid organizationId,
        Guid taskTemplateId,
        string title,
        int sortOrder,
        string? description = null,
        bool isRequired = false,
        EvidenceMode evidenceMode = EvidenceMode.None,
        TaskItemType itemType = TaskItemType.SingleLineText,
        string? options = null,
        string? mainBlockTitle = null,
        string? subBlockTitle = null,
        int maxAttachments = 5)
    {
        OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId));
        TaskTemplateId = Guard.NotEmpty(taskTemplateId, nameof(taskTemplateId));
        Update(title, description, sortOrder, isRequired, evidenceMode, itemType, options, mainBlockTitle, subBlockTitle, maxAttachments);
    }

    public Guid TaskTemplateId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsRequired { get; private set; }
    public EvidenceMode EvidenceMode { get; private set; }
    public TaskItemType ItemType { get; private set; } = TaskItemType.SingleLineText;
    public string? Options { get; private set; }
    public string? MainBlockTitle { get; private set; }
    public string? SubBlockTitle { get; private set; }
    public int MaxAttachments { get; private set; } = 5;
    public bool IsActive { get; private set; } = true;

    public void Update(
        string title,
        string? description,
        int sortOrder,
        bool isRequired,
        EvidenceMode evidenceMode,
        TaskItemType itemType = TaskItemType.SingleLineText,
        string? options = null,
        string? mainBlockTitle = null,
        string? subBlockTitle = null,
        int maxAttachments = 5)
    {
        string effectiveTitle = string.IsNullOrWhiteSpace(title)
            ? (!string.IsNullOrWhiteSpace(mainBlockTitle) ? mainBlockTitle : (!string.IsNullOrWhiteSpace(subBlockTitle) ? subBlockTitle : title))
            : title;
        Title = Guard.Required(effectiveTitle, nameof(title), 240);
        Description = Guard.Optional(description, 4000);
        SortOrder = Guard.NonNegative(sortOrder, nameof(sortOrder));
        IsRequired = isRequired;
        EvidenceMode = evidenceMode;
        ItemType = itemType;
        Options = Guard.Optional(options, 4000);
        MainBlockTitle = Guard.Optional(mainBlockTitle, 240);
        SubBlockTitle = Guard.Optional(subBlockTitle, 240);
        MaxAttachments = Math.Clamp(maxAttachments <= 0 ? 5 : maxAttachments, 1, 5);
    }

    public void Reorder(int sortOrder) => SortOrder = Guard.NonNegative(sortOrder, nameof(sortOrder));
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}

public sealed class TaskTemplateItemAttachment : TenantAuditableEntity
{
    private TaskTemplateItemAttachment() { }

    public TaskTemplateItemAttachment(
        Guid organizationId,
        Guid taskTemplateItemId,
        string fileUrl,
        string fileType,
        Guid uploadedBy,
        string? caption = null)
    {
        OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId));
        TaskTemplateItemId = Guard.NotEmpty(taskTemplateItemId, nameof(taskTemplateItemId));
        FileUrl = Guard.Required(fileUrl, nameof(fileUrl), 2000);
        FileType = Guard.Required(fileType, nameof(fileType), 120);
        UploadedBy = Guard.NotEmpty(uploadedBy, nameof(uploadedBy));
        Caption = Guard.Optional(caption, 500);
    }

    public Guid TaskTemplateItemId { get; private set; }
    public string FileUrl { get; private set; } = string.Empty;
    public string FileType { get; private set; } = string.Empty;
    public string? Caption { get; private set; }
    public Guid UploadedBy { get; private set; }
}

public sealed class TaskDistribution : TenantAuditableEntity
{
    private TaskDistribution() { }

    public TaskDistribution(
        Guid organizationId,
        Guid branchId,
        Guid departmentId,
        TaskAssignmentMode assignmentMode,
        DateOnly occurrenceDate,
        DateTimeOffset scheduledStartAt,
        DateTimeOffset dueAt,
        Guid createdBy,
        Guid? taskTemplateId = null,
        Guid? taskScheduleId = null)
    {
        if (dueAt <= scheduledStartAt)
        {
            throw new DomainInvariantException("Distribution due time must be later than its scheduled start time.");
        }

        if (!Enum.IsDefined(assignmentMode))
        {
            throw new DomainInvariantException("Assignment mode is not supported.");
        }

        ValidateOptionalId(taskTemplateId, nameof(taskTemplateId));
        ValidateOptionalId(taskScheduleId, nameof(taskScheduleId));
        OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId));
        BranchId = Guard.NotEmpty(branchId, nameof(branchId));
        DepartmentId = Guard.NotEmpty(departmentId, nameof(departmentId));
        TaskTemplateId = taskTemplateId;
        TaskScheduleId = taskScheduleId;
        AssignmentMode = assignmentMode;
        OccurrenceDate = occurrenceDate;
        ScheduledStartAt = scheduledStartAt;
        DueAt = dueAt;
        CreatedBy = Guard.NotEmpty(createdBy, nameof(createdBy));
    }

    public Guid BranchId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid? TaskTemplateId { get; private set; }
    public Guid? TaskScheduleId { get; private set; }
    public TaskAssignmentMode AssignmentMode { get; private set; }
    public DateOnly OccurrenceDate { get; private set; }
    public DateTimeOffset ScheduledStartAt { get; private set; }
    public DateTimeOffset DueAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    public void Reschedule(DateTimeOffset scheduledStartAt, DateTimeOffset dueAt)
    {
        if (dueAt <= scheduledStartAt)
        {
            throw new DomainInvariantException("Distribution due time must be later than its scheduled start time.");
        }

        ScheduledStartAt = scheduledStartAt;
        DueAt = dueAt;
    }

    public void UpdateDetails(Guid branchId, Guid departmentId, TaskAssignmentMode assignmentMode, Guid? taskTemplateId)
    {
        if (!Enum.IsDefined(assignmentMode))
        {
            throw new DomainInvariantException("Assignment mode is not supported.");
        }

        ValidateOptionalId(taskTemplateId, nameof(taskTemplateId));
        BranchId = Guard.NotEmpty(branchId, nameof(branchId));
        DepartmentId = Guard.NotEmpty(departmentId, nameof(departmentId));
        AssignmentMode = assignmentMode;
        TaskTemplateId = taskTemplateId;
    }

    private static void ValidateOptionalId(Guid? value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new DomainInvariantException($"{name} cannot be empty.");
        }
    }
}

public sealed class TaskScheduleAssignee : TenantAuditableEntity
{
    private TaskScheduleAssignee() { }

    public TaskScheduleAssignee(Guid organizationId, Guid taskScheduleId, Guid userId)
    {
        OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId));
        TaskScheduleId = Guard.NotEmpty(taskScheduleId, nameof(taskScheduleId));
        UserId = Guard.NotEmpty(userId, nameof(userId));
    }

    public Guid TaskScheduleId { get; private set; }
    public Guid UserId { get; private set; }
}

public sealed class TaskSchedule : TenantSoftDeletableEntity
{
    private Weekday[] _weekdays = [];
    private IReadOnlyList<Weekday>? _weekdaysView;
    private int[] _monthDays = [];
    private IReadOnlyList<int>? _monthDaysView;

    private TaskSchedule() { }

    public TaskSchedule(
        Guid organizationId,
        Guid taskTemplateId,
        Guid branchId,
        Guid departmentId,
        TaskAssignmentMode assignmentMode,
        RecurrenceType recurrenceType,
        DateOnly recurrenceStartDate,
        TimeOnly executionStartTime,
        TimeOnly executionDueTime,
        Guid createdBy,
        IEnumerable<Weekday>? weekdays = null,
        IEnumerable<int>? monthDays = null,
        bool includeLastDayOfMonth = false,
        DateOnly? recurrenceEndDate = null,
        int executionDueDayOffset = 0,
        bool isActive = true)
    {
        OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId));
        CreatedBy = Guard.NotEmpty(createdBy, nameof(createdBy));
        Update(
            taskTemplateId,
            branchId,
            departmentId,
            assignmentMode,
            recurrenceType,
            weekdays,
            monthDays,
            includeLastDayOfMonth,
            recurrenceStartDate,
            recurrenceEndDate,
            executionStartTime,
            executionDueTime,
            executionDueDayOffset);
        IsActive = isActive;
    }

    public Guid TaskTemplateId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public TaskAssignmentMode AssignmentMode { get; private set; }
    public RecurrenceType RecurrenceType { get; private set; }
    public IReadOnlyList<Weekday> Weekdays => _weekdaysView ??= Array.AsReadOnly(_weekdays);
    public IReadOnlyList<int> MonthDays => _monthDaysView ??= Array.AsReadOnly(_monthDays);
    public bool IncludeLastDayOfMonth { get; private set; }
    public DateOnly RecurrenceStartDate { get; private set; }
    public DateOnly? RecurrenceEndDate { get; private set; }
    public TimeOnly ExecutionStartTime { get; private set; }
    public TimeOnly ExecutionDueTime { get; private set; }
    public int ExecutionDueDayOffset { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid CreatedBy { get; private set; }
    public uint Version { get; private set; }

    public void Update(
        Guid taskTemplateId,
        Guid branchId,
        Guid departmentId,
        TaskAssignmentMode assignmentMode,
        RecurrenceType recurrenceType,
        IEnumerable<Weekday>? weekdays,
        IEnumerable<int>? monthDays,
        bool includeLastDayOfMonth,
        DateOnly recurrenceStartDate,
        DateOnly? recurrenceEndDate,
        TimeOnly executionStartTime,
        TimeOnly executionDueTime,
        int executionDueDayOffset)
    {
        TaskTemplateId = Guard.NotEmpty(taskTemplateId, nameof(taskTemplateId));
        BranchId = Guard.NotEmpty(branchId, nameof(branchId));
        DepartmentId = Guard.NotEmpty(departmentId, nameof(departmentId));
        if (!Enum.IsDefined(assignmentMode))
        {
            throw new DomainInvariantException("Assignment mode is not supported.");
        }

        AssignmentMode = assignmentMode;
        RecurrenceType = recurrenceType;
        _weekdays = weekdays?.Distinct().Order().ToArray() ?? [];
        _weekdaysView = null;
        _monthDays = monthDays?.Distinct().Order().ToArray() ?? [];
        _monthDaysView = null;
        IncludeLastDayOfMonth = includeLastDayOfMonth;
        RecurrenceStartDate = recurrenceStartDate;
        RecurrenceEndDate = recurrenceEndDate;
        ExecutionStartTime = executionStartTime;
        ExecutionDueTime = executionDueTime;
        ExecutionDueDayOffset = executionDueDayOffset;
        ValidateRecurrence();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void ValidateRecurrence()
    {
        if (!Enum.IsDefined(RecurrenceType))
        {
            throw new DomainInvariantException("Recurrence type is not supported.");
        }

        if (_weekdays.Any(weekday => !Enum.IsDefined(weekday)))
        {
            throw new DomainInvariantException("Schedule contains an invalid weekday.");
        }

        if (RecurrenceEndDate.HasValue && RecurrenceEndDate.Value < RecurrenceStartDate)
        {
            throw new DomainInvariantException("Schedule end date cannot precede its start date.");
        }

        if (ExecutionDueDayOffset is < 0 or > 1)
        {
            throw new DomainInvariantException("Due day offset must be 0 or 1.");
        }

        if (ExecutionDueDayOffset == 0 && ExecutionDueTime <= ExecutionStartTime)
        {
            throw new DomainInvariantException("Same-day schedule due time must be later than its start time.");
        }

        // Weekly: requires weekdays, no month days, no last-day
        if (RecurrenceType == RecurrenceType.Weekly && _weekdays.Length == 0)
        {
            throw new DomainInvariantException("Weekly recurrence requires at least one weekday.");
        }

        if (RecurrenceType != RecurrenceType.Weekly && _weekdays.Length != 0)
        {
            throw new DomainInvariantException("Weekdays are only valid for weekly recurrence.");
        }

        // Monthly: requires at least one month day OR last-day flag, no weekdays
        if (RecurrenceType == RecurrenceType.Monthly && _monthDays.Length == 0 && !IncludeLastDayOfMonth)
        {
            throw new DomainInvariantException("Monthly recurrence requires at least one month day or the last-day-of-month flag.");
        }

        if (RecurrenceType != RecurrenceType.Monthly && _monthDays.Length != 0)
        {
            throw new DomainInvariantException("Month days are only valid for monthly recurrence.");
        }

        if (RecurrenceType != RecurrenceType.Monthly && IncludeLastDayOfMonth)
        {
            throw new DomainInvariantException("Include last day of month is only valid for monthly recurrence.");
        }

        if (_monthDays.Any(day => day is < 1 or > 31))
        {
            throw new DomainInvariantException("Each month day must be between 1 and 31.");
        }
    }
}

public sealed class TaskScheduleDate : TenantAuditableEntity
{
    private TaskScheduleDate() { }

    public TaskScheduleDate(Guid organizationId, Guid taskScheduleId, DateOnly occurrenceDate)
    {
        OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId));
        TaskScheduleId = Guard.NotEmpty(taskScheduleId, nameof(taskScheduleId));
        OccurrenceDate = occurrenceDate;
    }

    public Guid TaskScheduleId { get; private set; }
    public DateOnly OccurrenceDate { get; private set; }
}

public sealed class OperationalTask : TenantSoftDeletableEntity
{
    private OperationalTask() { }

    public OperationalTask(
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

        OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId));
        BranchId = Guard.NotEmpty(branchId, nameof(branchId));
        DepartmentId = Guard.NotEmpty(departmentId, nameof(departmentId));
        Title = Guard.Required(title, nameof(title), 240);
        OccurrenceDate = occurrenceDate;
        ScheduledStartAt = scheduledStartAt;
        DueAt = dueAt;
        CreatedBy = Guard.NotEmpty(createdBy, nameof(createdBy));
    }

    public Guid BranchId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid? TaskDistributionId { get; private set; }
    public Guid? AssigneeUserId { get; private set; }
    public Guid? TaskTemplateId { get; private set; }
    public Guid? TaskScheduleId { get; private set; }
    public Guid? ParentTaskId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateOnly OccurrenceDate { get; private set; }
    public DateTimeOffset ScheduledStartAt { get; private set; }
    public DateTimeOffset DueAt { get; private set; }
    public TaskPriority Priority { get; private set; } = TaskPriority.Normal;
    public OperationalTaskStatus Status { get; private set; } = OperationalTaskStatus.NotStarted;
    public bool RequiresApproval { get; private set; }
    public bool IsScheduleOverride { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? SubmittedForApprovalAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public string? BlockedReason { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public string? CancellationReason { get; private set; }
    public uint Version { get; private set; }

    public bool IsOverdue(DateTimeOffset now) =>
        DueAt < now &&
        Status is not OperationalTaskStatus.Completed
            and not OperationalTaskStatus.Cancelled;

    public TaskExecutionWindowState GetExecutionWindowState(DateTimeOffset now)
    {
        if (now < ScheduledStartAt) return TaskExecutionWindowState.NotOpen;
        if (now > DueAt) return TaskExecutionWindowState.Expired;
        return TaskExecutionWindowState.Open;
    }

    public bool CanStartInWindow(DateTimeOffset now) =>
        (Status is OperationalTaskStatus.NotStarted or OperationalTaskStatus.Blocked or OperationalTaskStatus.Returned)
        && GetExecutionWindowState(now) == TaskExecutionWindowState.Open;

    public bool CanCompleteInWindow(DateTimeOffset now) =>
        (Status is OperationalTaskStatus.InProgress or OperationalTaskStatus.Returned)
        && GetExecutionWindowState(now) == TaskExecutionWindowState.Open;

    public static OperationalTask CreateAssignedCopy(
        Guid organizationId,
        Guid taskDistributionId,
        Guid branchId,
        Guid departmentId,
        Guid assigneeUserId,
        Guid? taskTemplateId,
        Guid? taskScheduleId,
        Guid? parentTaskId,
        string title,
        string? description,
        DateOnly occurrenceDate,
        DateTimeOffset scheduledStartAt,
        DateTimeOffset dueAt,
        TaskPriority priority,
        bool requiresApproval,
        Guid createdBy)
    {
        OperationalTask task = new(
            organizationId,
            branchId,
            departmentId,
            title,
            occurrenceDate,
            scheduledStartAt,
            dueAt,
            createdBy);
        task.TaskDistributionId = Guard.NotEmpty(taskDistributionId, nameof(taskDistributionId));
        task.Configure(
            taskTemplateId,
            taskScheduleId,
            parentTaskId,
            Guard.NotEmpty(assigneeUserId, nameof(assigneeUserId)),
            description,
            priority,
            requiresApproval);
        return task;
    }

    public void Configure(
        Guid? taskTemplateId,
        Guid? taskScheduleId,
        Guid? parentTaskId,
        Guid? assigneeUserId,
        string? description,
        TaskPriority priority,
        bool requiresApproval)
    {
        EnsureEditable();

        ValidateOptionalId(taskTemplateId, nameof(taskTemplateId));
        ValidateOptionalId(taskScheduleId, nameof(taskScheduleId));
        ValidateOptionalId(parentTaskId, nameof(parentTaskId));
        ValidateOptionalId(assigneeUserId, nameof(assigneeUserId));
        TaskTemplateId = taskTemplateId;
        TaskScheduleId = taskScheduleId;
        ParentTaskId = parentTaskId;
        AssigneeUserId = assigneeUserId;
        Description = Guard.Optional(description, 4000);
        Priority = priority;
        RequiresApproval = requiresApproval;
    }


    public void UpdateDetails(string title, string? description, TaskPriority priority, bool requiresApproval)
    {
        EnsureEditable();

        Title = Guard.Required(title, nameof(title), 240);
        Description = Guard.Optional(description, 4000);
        Priority = priority;
        RequiresApproval = requiresApproval;
    }

    public void Assign(Guid assigneeUserId)
    {
        EnsureAssignmentAllowed();

        AssigneeUserId = Guard.NotEmpty(assigneeUserId, nameof(assigneeUserId));
    }

    public void Reschedule(DateOnly occurrenceDate, DateTimeOffset scheduledStartAt, DateTimeOffset dueAt, bool isOverride)
    {
        EnsureEditable();

        if (dueAt <= scheduledStartAt)
        {
            throw new DomainInvariantException("Task due time must be later than its scheduled start time.");
        }

        OccurrenceDate = occurrenceDate;
        ScheduledStartAt = scheduledStartAt;
        DueAt = dueAt;
        IsScheduleOverride = isOverride;
    }

    public TaskTransition Start(Guid actorId, DateTimeOffset occurredAt) =>
        Transition(OperationalTaskStatus.InProgress, actorId, occurredAt, null, task =>
        {
            task.StartedAt ??= occurredAt;
            task.BlockedReason = null;
        });

    public TaskTransition Block(Guid actorId, DateTimeOffset occurredAt, string reason)
    {
        string validReason = Guard.Required(reason, nameof(reason), 1000);
        return Transition(OperationalTaskStatus.Blocked, actorId, occurredAt, validReason, task =>
            task.BlockedReason = validReason);
    }

    public TaskTransition Resume(Guid actorId, DateTimeOffset occurredAt) =>
        Transition(OperationalTaskStatus.InProgress, actorId, occurredAt, null, task =>
        {
            task.StartedAt ??= occurredAt;
            task.BlockedReason = null;
        });

    public TaskTransition Complete(Guid actorId, DateTimeOffset occurredAt, bool allRequiredItemsCompleted)
    {
        EnsureChecklistComplete(allRequiredItemsCompleted);
        if (RequiresApproval)
        {
            throw new DomainInvariantException("A task requiring approval must be submitted for approval.");
        }

        return Transition(OperationalTaskStatus.Completed, actorId, occurredAt, null, task =>
            task.CompletedAt = occurredAt);
    }

    public TaskTransition SubmitForApproval(Guid actorId, DateTimeOffset occurredAt, bool allRequiredItemsCompleted)
    {
        EnsureChecklistComplete(allRequiredItemsCompleted);
        if (!RequiresApproval)
        {
            throw new DomainInvariantException("A task without approval cannot be submitted for approval.");
        }

        return Transition(OperationalTaskStatus.PendingApproval, actorId, occurredAt, null, task =>
            task.SubmittedForApprovalAt = occurredAt);
    }

    public TaskTransition Approve(Guid actorId, DateTimeOffset occurredAt) =>
        Transition(OperationalTaskStatus.Completed, actorId, occurredAt, null, task =>
        {
            task.CompletedAt = occurredAt;
            task.ApprovedAt = occurredAt;
            task.ApprovedBy = actorId;
        });

    public TaskTransition ReturnForCorrection(Guid actorId, DateTimeOffset occurredAt, string reason)
    {
        string validReason = Guard.Required(reason, nameof(reason), 1000);
        return Transition(OperationalTaskStatus.Returned, actorId, occurredAt, validReason, static task =>
        {
            task.CompletedAt = null;
            task.ApprovedAt = null;
            task.ApprovedBy = null;
        });
    }

    public TaskTransition ResetToNotStarted(Guid actorId, DateTimeOffset occurredAt, string reason)
    {
        Guard.NotEmpty(actorId, nameof(actorId));
        OperationalTaskStatus oldStatus = Status;
        Status = OperationalTaskStatus.NotStarted;
        CancelledAt = null;
        CancelledBy = null;
        CancellationReason = null;
        return new TaskTransition(oldStatus, OperationalTaskStatus.NotStarted, actorId, occurredAt, reason);
    }

    public TaskTransition Cancel(Guid actorId, DateTimeOffset occurredAt, string reason)
    {
        string validReason = Guard.Required(reason, nameof(reason), 1000);
        return Transition(OperationalTaskStatus.Cancelled, actorId, occurredAt, validReason, task =>
        {
            task.CancelledAt = occurredAt;
            task.CancelledBy = actorId;
            task.CancellationReason = validReason;
        });
    }

    private TaskTransition Transition(
        OperationalTaskStatus target,
        Guid actorId,
        DateTimeOffset occurredAt,
        string? reason,
        Action<OperationalTask> apply)
    {
        Guard.NotEmpty(actorId, nameof(actorId));
        OperationalTaskStatus oldStatus = Status;
        bool allowed = (oldStatus, target) switch
        {
            (OperationalTaskStatus.NotStarted, OperationalTaskStatus.InProgress or OperationalTaskStatus.Cancelled) => true,
            (OperationalTaskStatus.InProgress, OperationalTaskStatus.Blocked or OperationalTaskStatus.Completed
                or OperationalTaskStatus.PendingApproval or OperationalTaskStatus.Cancelled) => true,
            (OperationalTaskStatus.Blocked, OperationalTaskStatus.InProgress or OperationalTaskStatus.Cancelled) => true,
            (OperationalTaskStatus.PendingApproval, OperationalTaskStatus.Completed
                or OperationalTaskStatus.Returned or OperationalTaskStatus.Cancelled) => true,
            (OperationalTaskStatus.Returned, OperationalTaskStatus.InProgress or OperationalTaskStatus.Cancelled) => true,
            _ => false,
        };
        if (!allowed)
        {
            throw new InvalidStateTransitionException(nameof(OperationalTask), oldStatus.ToString(), target.ToString());
        }

        apply(this);
        Status = target;
        return new TaskTransition(oldStatus, target, actorId, occurredAt, reason);
    }

    private static void ValidateOptionalId(Guid? value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new DomainInvariantException($"{name} cannot be empty.");
        }
    }

    private static void EnsureChecklistComplete(bool completed)
    {
        if (!completed)
        {
            throw new DomainInvariantException("All required checklist items must be completed.");
        }
    }

    private void EnsureEditable()
    {
        if (Status is not OperationalTaskStatus.NotStarted)
        {
            throw new DomainInvariantException(
                "Only a task that has not started can be edited.");
        }
    }

    private void EnsureAssignmentAllowed()
    {
        if (Status is OperationalTaskStatus.Completed
            or OperationalTaskStatus.Cancelled
            or OperationalTaskStatus.PendingApproval)
        {
            throw new DomainInvariantException(
                "The task cannot be reassigned in its current state.");
        }
    }
}

public sealed class TaskItem : TenantAuditableEntity
{
    private TaskItem() { }

    public TaskItem(
        Guid organizationId,
        Guid taskId,
        string title,
        int sortOrder,
        bool isRequired,
        EvidenceMode evidenceMode,
        Guid? templateItemId = null,
        string? description = null,
        TaskItemType itemType = TaskItemType.SingleLineText,
        string? options = null,
        string? mainBlockTitle = null,
        string? subBlockTitle = null,
        int maxAttachments = 5)
    {
        OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId));
        TaskId = Guard.NotEmpty(taskId, nameof(taskId));
        if (templateItemId == Guid.Empty)
        {
            throw new DomainInvariantException("Template item identifier cannot be empty.");
        }

        TemplateItemId = templateItemId;
        string effectiveTitle = string.IsNullOrWhiteSpace(title)
            ? (!string.IsNullOrWhiteSpace(mainBlockTitle) ? mainBlockTitle : (!string.IsNullOrWhiteSpace(subBlockTitle) ? subBlockTitle : title))
            : title;
        Title = Guard.Required(effectiveTitle, nameof(title), 240);
        Description = Guard.Optional(description, 4000);
        SortOrder = Guard.NonNegative(sortOrder, nameof(sortOrder));
        IsRequired = isRequired;
        EvidenceMode = evidenceMode;
        ItemType = itemType;
        Options = Guard.Optional(options, 4000);
        MainBlockTitle = Guard.Optional(mainBlockTitle, 240);
        SubBlockTitle = Guard.Optional(subBlockTitle, 240);
        MaxAttachments = Math.Clamp(maxAttachments <= 0 ? 5 : maxAttachments, 1, 5);
    }

    public void Update(
        string title,
        int sortOrder,
        bool isRequired,
        EvidenceMode evidenceMode,
        string? description = null,
        TaskItemType itemType = TaskItemType.SingleLineText,
        string? options = null,
        string? mainBlockTitle = null,
        string? subBlockTitle = null,
        int maxAttachments = 5)
    {
        string effectiveTitle = string.IsNullOrWhiteSpace(title)
            ? (!string.IsNullOrWhiteSpace(mainBlockTitle) ? mainBlockTitle : (!string.IsNullOrWhiteSpace(subBlockTitle) ? subBlockTitle : title))
            : title;
        Title = Guard.Required(effectiveTitle, nameof(title), 240);
        Description = Guard.Optional(description, 4000);
        SortOrder = Guard.NonNegative(sortOrder, nameof(sortOrder));
        IsRequired = isRequired;
        EvidenceMode = evidenceMode;
        ItemType = itemType;
        Options = Guard.Optional(options, 4000);
        MainBlockTitle = Guard.Optional(mainBlockTitle, 240);
        SubBlockTitle = Guard.Optional(subBlockTitle, 240);
        MaxAttachments = Math.Clamp(maxAttachments <= 0 ? 5 : maxAttachments, 1, 5);
    }

    public Guid TaskId { get; private set; }
    public Guid? TemplateItemId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsRequired { get; private set; }
    public EvidenceMode EvidenceMode { get; private set; }
    public TaskItemType ItemType { get; private set; } = TaskItemType.SingleLineText;
    public string? Options { get; private set; }
    public string? MainBlockTitle { get; private set; }
    public string? SubBlockTitle { get; private set; }
    public int MaxAttachments { get; private set; } = 5;
    public string? Value { get; private set; }
    public TaskItemStatus Status { get; private set; } = TaskItemStatus.Pending;
    public Guid? CompletedBy { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? Note { get; private set; }
    public uint Version { get; private set; }

    public void SetNote(string? note) => Note = Guard.Optional(note, 2000);
    public void SetValue(string? value) => Value = Guard.Optional(value, 4000);

    public void Complete(Guid completedBy, DateTimeOffset completedAt, bool hasEvidenceAttachment)
    {
        //if (Status != TaskItemStatus.Pending)
        //{
        //    throw new InvalidStateTransitionException(nameof(TaskItem), Status.ToString(), TaskItemStatus.Completed.ToString());
        //}
       

        if (EvidenceMode == EvidenceMode.Required && !hasEvidenceAttachment)
        {
            throw new DomainInvariantException("An item with required evidence needs an attachment before completion.");
        }

        CompletedBy = Guard.NotEmpty(completedBy, nameof(completedBy));
        CompletedAt = completedAt;
        Status = TaskItemStatus.Completed;
    }

    public void Reset()
    {
        if (Status is not (TaskItemStatus.Completed or TaskItemStatus.Skipped))
        {
            throw new InvalidStateTransitionException(nameof(TaskItem), Status.ToString(), TaskItemStatus.Pending.ToString());
        }

        Status = TaskItemStatus.Pending;
        CompletedBy = null;
        CompletedAt = null;
    }

    public void Skip()
    {
        if (Status != TaskItemStatus.Pending)
        {
            throw new InvalidStateTransitionException(nameof(TaskItem), Status.ToString(), TaskItemStatus.Skipped.ToString());
        }

        if (IsRequired)
        {
            throw new DomainInvariantException("A required task item cannot be skipped.");
        }

        Status = TaskItemStatus.Skipped;
    }
}

public sealed class TaskAttachment : TenantAuditableEntity
{
    private TaskAttachment() { }

    public TaskAttachment(
        Guid organizationId,
        Guid taskId,
        Guid? taskItemId,
        Guid uploadedBy,
        string fileUrl,
        string fileType,
        AttachmentType attachmentType,
        string? caption = null)
    {
        OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId));
        TaskId = Guard.NotEmpty(taskId, nameof(taskId));
        if (taskItemId == Guid.Empty)
        {
            throw new DomainInvariantException("Task item identifier cannot be empty.");
        }

        TaskItemId = taskItemId;
        UploadedBy = Guard.NotEmpty(uploadedBy, nameof(uploadedBy));
        FileUrl = Guard.Required(fileUrl, nameof(fileUrl), 2000);
        FileType = Guard.Required(fileType, nameof(fileType), 120);
        AttachmentType = attachmentType;
        Caption = Guard.Optional(caption, 500);
    }

    public Guid TaskId { get; private set; }
    public Guid? TaskItemId { get; private set; }
    public Guid UploadedBy { get; private set; }
    public string FileUrl { get; private set; } = string.Empty;
    public string FileType { get; private set; } = string.Empty;
    public AttachmentType AttachmentType { get; private set; }
    public string? Caption { get; private set; }
}

public sealed class TaskStatusHistory : TenantAuditableEntity
{
    private TaskStatusHistory() { }

    private TaskStatusHistory(
        Guid organizationId,
        Guid taskId,
        OperationalTaskStatus? oldStatus,
        OperationalTaskStatus newStatus,
        Guid changedBy,
        DateTimeOffset occurredAt,
        string? reason)
    {
        OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId));
        TaskId = Guard.NotEmpty(taskId, nameof(taskId));
        OldStatus = oldStatus;
        NewStatus = newStatus;
        ChangedBy = Guard.NotEmpty(changedBy, nameof(changedBy));
        OccurredAt = Guard.NotDefault(occurredAt, nameof(occurredAt));
        Reason = Guard.Optional(reason, 1000);
    }

    public Guid TaskId { get; private set; }
    public OperationalTaskStatus? OldStatus { get; private set; }
    public OperationalTaskStatus NewStatus { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? Reason { get; private set; }

    public static TaskStatusHistory FromTransition(
        Guid organizationId,
        Guid taskId,
        TaskTransition transition) =>
        new(
            organizationId,
            taskId,
            transition.OldStatus,
            transition.NewStatus,
            transition.ActorId,
            transition.OccurredAt,
            transition.Reason);

    public static TaskStatusHistory Created(
        Guid organizationId,
        Guid taskId,
        Guid actorId,
        DateTimeOffset occurredAt,
        string? reason = null) =>
        new(
            organizationId,
            taskId,
            null,
            OperationalTaskStatus.NotStarted,
            actorId,
            occurredAt,
            reason);
}

public sealed class TaskAssignmentHistory : TenantAuditableEntity
{
    private TaskAssignmentHistory() { }

    public TaskAssignmentHistory(
        Guid organizationId,
        Guid taskId,
        Guid? previousAssigneeUserId,
        Guid newAssigneeUserId,
        Guid changedBy,
        DateTimeOffset occurredAt)
    {
        if (previousAssigneeUserId == Guid.Empty)
        {
            throw new DomainInvariantException("Previous assignee identifier cannot be empty.");
        }

        OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId));
        TaskId = Guard.NotEmpty(taskId, nameof(taskId));
        PreviousAssigneeUserId = previousAssigneeUserId;
        NewAssigneeUserId = Guard.NotEmpty(newAssigneeUserId, nameof(newAssigneeUserId));
        ChangedBy = Guard.NotEmpty(changedBy, nameof(changedBy));
        OccurredAt = Guard.NotDefault(occurredAt, nameof(occurredAt));
    }

    public Guid TaskId { get; private set; }
    public Guid? PreviousAssigneeUserId { get; private set; }
    public Guid NewAssigneeUserId { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
