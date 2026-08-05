using OpsManager.Domain.Enums;
using OpsManager.Service.Common;

namespace OpsManager.Service.Tasks.DTOs;

public sealed record ChecklistDefinitionRequest(
    string Title,
    string? Description,
    int SortOrder,
    bool IsRequired,
    EvidenceMode EvidenceMode,
    TaskItemType ItemType = TaskItemType.SingleLineText,
    string? Options = null,
    string? MainBlockTitle = null,
    string? SubBlockTitle = null);

public sealed record ChecklistDefinitionDto(
    Guid Id,
    string Title,
    string? Description,
    int SortOrder,
    bool IsRequired,
    EvidenceMode EvidenceMode,
    TaskItemType ItemType = TaskItemType.SingleLineText,
    string? Options = null,
    string? MainBlockTitle = null,
    string? SubBlockTitle = null);

public sealed record SaveTaskTemplateRequest(
    Guid? DefaultDepartmentId,
    string Title,
    string? Description,
    TaskPriority DefaultPriority,
    int? DefaultDurationMinutes,
    bool RequiresApproval,
    bool IsActive,
    IReadOnlyList<ChecklistDefinitionRequest> Items);

public sealed record TaskTemplateDto(
    Guid Id,
    Guid? DefaultDepartmentId,
    string Title,
    string? Description,
    TaskPriority DefaultPriority,
    int? DefaultDurationMinutes,
    bool RequiresApproval,
    bool IsActive,
    IReadOnlyList<ChecklistDefinitionDto> Items);

public sealed record ReorderItemsRequest(IReadOnlyList<Guid> ItemIds);

public sealed record TaskAttachmentDto(Guid Id, string FileUrl, string FileType);

public sealed record TaskItemDto(
    Guid Id,
    string Title,
    string? Description,
    int SortOrder,
    bool IsRequired,
    EvidenceMode EvidenceMode,
    TaskItemStatus Status,
    Guid? CompletedBy,
    DateTimeOffset? CompletedAt,
    string? Note,
    int AttachmentCount,
    TaskItemType ItemType = TaskItemType.SingleLineText,
    string? Options = null,
    string? MainBlockTitle = null,
    string? SubBlockTitle = null,
    string? Value = null,
    IReadOnlyList<TaskAttachmentDto>? Attachments = null);

public sealed record TaskDto(
    Guid Id,
    Guid BranchId,
    Guid DepartmentId,
    Guid? TaskDistributionId,
    Guid? AssigneeUserId,
    string? AssigneeName,
    Guid? TaskTemplateId,
    Guid? TaskScheduleId,
    Guid? ParentTaskId,
    string Title,
    string? Description,
    DateOnly OccurrenceDate,
    DateTimeOffset ScheduledStartAt,
    DateTimeOffset DueAt,
    TaskPriority Priority,
    OperationalTaskStatus Status,
    bool RequiresApproval,
    bool IsOverdue,
    DateTimeOffset? StartedAt,
    DateTimeOffset? SubmittedForApprovalAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ApprovedAt,
    Guid? ApprovedBy,
    string? BlockedReason,
    IReadOnlyList<TaskItemDto> Items,
    TaskExecutionWindowState ExecutionWindowState = TaskExecutionWindowState.Open,
    bool CanStart = false,
    bool CanComplete = false,
    string? DepartmentName = null,
    string? CancellationReason = null);

public sealed record TaskAssignmentRequest(
    TaskAssignmentMode Mode,
    IReadOnlyList<Guid> UserIds);

public sealed record DistributedTaskResponse(
    Guid TaskId,
    Guid AssigneeUserId,
    string AssigneeName,
    OperationalTaskStatus Status);

public sealed record TaskDistributionResponse(
    Guid DistributionId,
    TaskAssignmentMode AssignmentMode,
    int CreatedTaskCount,
    IReadOnlyList<DistributedTaskResponse> Tasks);

public sealed record CreateTaskRequest(
    Guid BranchId,
    Guid DepartmentId,
    TaskAssignmentRequest Assignment,
    string Title,
    string? Description,
    DateTimeOffset ScheduledStartAt,
    DateTimeOffset DueAt,
    TaskPriority Priority,
    bool RequiresApproval,
    IReadOnlyList<ChecklistDefinitionRequest> Items);

public sealed record CreateTaskFromTemplateRequest(
    Guid? DepartmentId,
    TaskAssignmentRequest Assignment,
    DateTimeOffset ScheduledStartAt,
    DateTimeOffset? DueAt,
    TaskPriority? Priority,
    bool? RequiresApproval,
    IReadOnlyList<ChecklistDefinitionRequest>? Items);

public sealed record UpdateTaskRequest(
    string Title,
    string? Description,
    DateTimeOffset ScheduledStartAt,
    DateTimeOffset DueAt,
    TaskPriority Priority);

public sealed record AssignTaskRequest(Guid AssigneeUserId);
public sealed record CloneTaskRequest(TaskAssignmentRequest Assignment);
public sealed record ReasonRequest(string Reason);
public sealed record UpdateTaskItemRequest(TaskItemStatus Status, string? Note, string? Value = null);

public sealed record TaskQuery(
    int Page = 1,
    int PageSize = 20,
    TaskTemporalScope? Scope = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    OperationalTaskStatus? Status = null,
    Guid? BranchId = null,
    Guid? DepartmentId = null,
    Guid? AssigneeUserId = null,
    TaskPriority? Priority = null,
    bool? Overdue = null,
    Guid? TemplateId = null,
    Guid? ScheduleId = null,
    Guid? DistributionId = null,
    string? Search = null)
{
    public PageQuery PageQuery => new(Page, PageSize);
}

public sealed record SaveTaskScheduleRequest(
    Guid TaskTemplateId,
    Guid BranchId,
    Guid DepartmentId,
    TaskAssignmentRequest Assignment,
    RecurrenceType RecurrenceType,
    IReadOnlyList<Weekday> Weekdays,
    IReadOnlyList<int> MonthDays,
    bool IncludeLastDayOfMonth,
    IReadOnlyList<DateOnly> SpecificDates,
    DateOnly RecurrenceStartDate,
    DateOnly? RecurrenceEndDate,
    TimeOnly ExecutionStartTime,
    TimeOnly ExecutionDueTime,
    int ExecutionDueDayOffset,
    bool IsActive);

public sealed record TaskScheduleDto(
    Guid Id,
    Guid TaskTemplateId,
    Guid BranchId,
    Guid DepartmentId,
    TaskAssignmentMode AssignmentMode,
    IReadOnlyList<Guid> AssigneeUserIds,
    RecurrenceType RecurrenceType,
    IReadOnlyList<Weekday> Weekdays,
    IReadOnlyList<int> MonthDays,
    bool IncludeLastDayOfMonth,
    IReadOnlyList<DateOnly> SpecificDates,
    DateOnly RecurrenceStartDate,
    DateOnly? RecurrenceEndDate,
    TimeOnly ExecutionStartTime,
    TimeOnly ExecutionDueTime,
    int ExecutionDueDayOffset,
    bool IsActive);

public sealed record GenerateOccurrencesRequest(DateOnly? ThroughDate);
public sealed record OccurrenceGenerationResult(Guid ScheduleId, int CreatedCount, DateOnly ThroughDate);
