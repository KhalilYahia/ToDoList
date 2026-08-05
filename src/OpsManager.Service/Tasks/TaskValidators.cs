using OpsManager.Domain.Enums;
using OpsManager.Service.Common;
using OpsManager.Service.Tasks.DTOs;

namespace OpsManager.Service.Tasks;

public sealed class SaveTaskTemplateValidator : RequestValidator<SaveTaskTemplateRequest>
{
    protected override void Validate(SaveTaskTemplateRequest request)
    {
        Required(request.Title, nameof(request.Title), 240);
        Optional(request.Description, nameof(request.Description), 4000);
        if (request.DefaultDepartmentId == Guid.Empty)
        {
            Add(nameof(request.DefaultDepartmentId), "DefaultDepartmentId cannot be empty when supplied.");
        }

        if (request.DefaultDurationMinutes is <= 0)
        {
            Add(nameof(request.DefaultDurationMinutes), "DefaultDurationMinutes must be positive.");
        }

        ValidateItems(request.Items);
    }

    internal static void ValidateItems(IReadOnlyList<ChecklistDefinitionRequest> items)
    {
        if (items.Select(item => item.SortOrder).Distinct().Count() != items.Count)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                [nameof(items)] = ["Checklist sort order values must be unique."],
            });
        }

        if (items.Any(item =>
            string.IsNullOrWhiteSpace(item.Title) &&
            string.IsNullOrWhiteSpace(item.MainBlockTitle) &&
            string.IsNullOrWhiteSpace(item.SubBlockTitle)))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                [nameof(items)] = ["Every checklist item requires a title or block name."],
            });
        }
    }
}

public sealed class CreateTaskValidator : RequestValidator<CreateTaskRequest>
{
    protected override void Validate(CreateTaskRequest request)
    {
        Required(request.Title, nameof(request.Title), 240);
        Optional(request.Description, nameof(request.Description), 4000);
        if (request.BranchId == Guid.Empty || request.DepartmentId == Guid.Empty)
        {
            Add("scope", "BranchId and DepartmentId are required.");
        }

        if (request.DueAt <= request.ScheduledStartAt)
        {
            Add(nameof(request.DueAt), "DueAt must be later than ScheduledStartAt.");
        }

        ValidateAssignment(request.Assignment);
        SaveTaskTemplateValidator.ValidateItems(request.Items);
    }

    internal static void ValidateAssignment(TaskAssignmentRequest assignment)
    {
        if (!Enum.IsDefined(assignment.Mode))
        {
            throw Validation(nameof(assignment.Mode), "Assignment mode is not supported.");
        }

        if (assignment.UserIds.Any(userId => userId == Guid.Empty))
        {
            throw Validation(nameof(assignment.UserIds), "Assignee identifiers cannot be empty.");
        }

        if (assignment.UserIds.Distinct().Count() != assignment.UserIds.Count)
        {
            throw Validation(nameof(assignment.UserIds), "Duplicate assignee identifiers are not allowed.");
        }

        bool validCount = assignment.Mode switch
        {
            TaskAssignmentMode.SingleUser => assignment.UserIds.Count == 1,
            TaskAssignmentMode.SelectedUsers => assignment.UserIds.Count >= 2,
            TaskAssignmentMode.AllDepartmentMembers => assignment.UserIds.Count == 0,
            _ => false,
        };
        if (!validCount)
        {
            throw Validation(
                nameof(assignment.UserIds),
                "SingleUser requires one user, SelectedUsers requires at least two, and AllDepartmentMembers requires none.");
        }
    }

    private static RequestValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

public sealed class SaveTaskScheduleValidator : RequestValidator<SaveTaskScheduleRequest>
{
    protected override void Validate(SaveTaskScheduleRequest request)
    {
        if (request.TaskTemplateId == Guid.Empty || request.BranchId == Guid.Empty || request.DepartmentId == Guid.Empty)
        {
            Add("scope", "TaskTemplateId, BranchId, and DepartmentId are required.");
        }

        CreateTaskValidator.ValidateAssignment(request.Assignment);

        if (request.ExecutionDueDayOffset is < 0 or > 1)
        {
            Add(nameof(request.ExecutionDueDayOffset), "ExecutionDueDayOffset must be 0 or 1.");
        }

        if (request.ExecutionDueDayOffset == 0 && request.ExecutionDueTime <= request.ExecutionStartTime)
        {
            Add(nameof(request.ExecutionDueTime), "Same-day ExecutionDueTime must be later than ExecutionStartTime.");
        }

        if (request.RecurrenceType == RecurrenceType.Weekly && request.Weekdays.Count == 0)
        {
            Add(nameof(request.Weekdays), "Weekly recurrence requires at least one weekday.");
        }

        if (request.RecurrenceType != RecurrenceType.Weekly && request.Weekdays.Count != 0)
        {
            Add(nameof(request.Weekdays), "Weekdays are only valid for weekly recurrence.");
        }

        if (request.RecurrenceType == RecurrenceType.Monthly && request.MonthDays.Count == 0 && !request.IncludeLastDayOfMonth)
        {
            Add(nameof(request.MonthDays), "Monthly recurrence requires at least one month day or the last-day-of-month flag.");
        }

        if (request.RecurrenceType != RecurrenceType.Monthly && request.MonthDays.Count != 0)
        {
            Add(nameof(request.MonthDays), "MonthDays are only valid for monthly recurrence.");
        }

        if (request.RecurrenceType != RecurrenceType.Monthly && request.IncludeLastDayOfMonth)
        {
            Add(nameof(request.IncludeLastDayOfMonth), "IncludeLastDayOfMonth is only valid for monthly recurrence.");
        }

        if (request.MonthDays.Any(day => day is < 1 or > 31))
        {
            Add(nameof(request.MonthDays), "Each month day must be between 1 and 31.");
        }

        if (request.RecurrenceType == RecurrenceType.SpecificDates && request.SpecificDates.Count == 0)
        {
            Add(nameof(request.SpecificDates), "SpecificDates recurrence requires at least one date.");
        }

        if (request.RecurrenceType != RecurrenceType.SpecificDates && request.SpecificDates.Count != 0)
        {
            Add(nameof(request.SpecificDates), "SpecificDates are only valid for SpecificDates recurrence.");
        }
    }
}
