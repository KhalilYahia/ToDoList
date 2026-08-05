using OpsManager.Domain.Common;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;

namespace OpsManager.Domain.Tests;

public sealed class DomainInvariantTests
{
    [Fact]
    public void Organization_rejects_unsupported_default_language()
    {
        Assert.Throws<DomainInvariantException>(() => new Organization("Acme", "UTC", "de"));
    }

    [Fact]
    public void User_normalizes_email_for_unique_login()
    {
        User user = new("Manager", "Manager@Example.com", "already-hashed", "en");

        Assert.Equal("MANAGER@EXAMPLE.COM", user.NormalizedEmail);
    }

    [Fact]
    public void Operational_task_rejects_due_time_that_is_not_after_start()
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;

        Assert.Throws<DomainInvariantException>(() => new OperationalTask(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Invalid task",
            DateOnly.FromDateTime(start.UtcDateTime),
            start,
            start,
            Guid.NewGuid()));
    }

    [Fact]
    public void Operational_task_rejects_invalid_state_transition()
    {
        OperationalTask task = CreateTask();

        Assert.Throws<InvalidStateTransitionException>(() =>
            task.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Approval_workflow_sets_submission_and_final_completion_timestamps_correctly()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid actor = Guid.NewGuid();
        OperationalTask task = CreateTask(requiresApproval: true);

        task.Start(actor, now);
        task.SubmitForApproval(actor, now.AddMinutes(5), allRequiredItemsCompleted: true);

        Assert.Equal(OperationalTaskStatus.PendingApproval, task.Status);
        Assert.Equal(now.AddMinutes(5), task.SubmittedForApprovalAt);
        Assert.Null(task.CompletedAt);

        task.Approve(actor, now.AddMinutes(10));

        Assert.Equal(now.AddMinutes(10), task.CompletedAt);
        Assert.Equal(now.AddMinutes(10), task.ApprovedAt);
        Assert.Equal(actor, task.ApprovedBy);
    }

    [Fact]
    public void Returned_task_preserves_submission_audit_and_clears_final_fields()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid actor = Guid.NewGuid();
        OperationalTask task = CreateTask(requiresApproval: true);
        task.Start(actor, now);
        task.SubmitForApproval(actor, now.AddMinutes(5), true);

        task.ReturnForCorrection(actor, now.AddMinutes(6), "Fix the evidence");

        Assert.Equal(OperationalTaskStatus.Returned, task.Status);
        Assert.Equal(now.AddMinutes(5), task.SubmittedForApprovalAt);
        Assert.Null(task.CompletedAt);
        Assert.Null(task.ApprovedAt);
        Assert.Null(task.ApprovedBy);
    }

    [Fact]
    public void Completion_and_submission_require_all_required_items()
    {
        OperationalTask direct = CreateTask();
        direct.Start(Guid.NewGuid(), DateTimeOffset.UtcNow);
        OperationalTask approval = CreateTask(requiresApproval: true);
        approval.Start(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainInvariantException>(() =>
            direct.Complete(Guid.NewGuid(), DateTimeOffset.UtcNow, false));
        Assert.Throws<DomainInvariantException>(() =>
            approval.SubmitForApproval(Guid.NewGuid(), DateTimeOffset.UtcNow, false));
    }

    [Fact]
    public void Evidence_requirement_depends_only_on_evidence_mode()
    {
        Guid actor = Guid.NewGuid();
        TaskItem requiredEvidence = CreateItem(isRequired: false, EvidenceMode.Required);
        TaskItem optionalEvidence = CreateItem(isRequired: true, EvidenceMode.Optional);

        Assert.Throws<DomainInvariantException>(() =>
            requiredEvidence.Complete(actor, DateTimeOffset.UtcNow, false));
        optionalEvidence.Complete(actor, DateTimeOffset.UtcNow, false);

        Assert.Equal(TaskItemStatus.Completed, optionalEvidence.Status);
    }

    [Fact]
    public void Task_item_rejects_repeated_completion_and_invalid_skip_or_reset()
    {
        TaskItem completed = CreateItem(false, EvidenceMode.None);
        completed.Complete(Guid.NewGuid(), DateTimeOffset.UtcNow, false);
        TaskItem required = CreateItem(true, EvidenceMode.None);
        TaskItem pending = CreateItem(false, EvidenceMode.None);

        Assert.Throws<InvalidStateTransitionException>(() =>
            completed.Complete(Guid.NewGuid(), DateTimeOffset.UtcNow, false));
        Assert.Throws<DomainInvariantException>(required.Skip);
        Assert.Throws<InvalidStateTransitionException>(pending.Reset);
    }

    [Fact]
    public void New_task_item_starts_pending_without_relying_on_enum_order()
    {
        TaskItem item = CreateItem(false, EvidenceMode.None);

        Assert.Equal(TaskItemStatus.Pending, item.Status);
    }

    [Fact]
    public void Assigned_task_copies_have_independent_execution_and_checklist_state()
    {
        Guid organizationId = Guid.NewGuid();
        Guid distributionId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();
        Guid departmentId = Guid.NewGuid();
        Guid firstUser = Guid.NewGuid();
        Guid secondUser = Guid.NewGuid();
        Guid creator = Guid.NewGuid();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        OperationalTask first = OperationalTask.CreateAssignedCopy(
            organizationId,
            distributionId,
            branchId,
            departmentId,
            firstUser,
            null,
            null,
            null,
            "Independent task",
            null,
            DateOnly.FromDateTime(start.UtcDateTime),
            start,
            start.AddHours(1),
            TaskPriority.Normal,
            false,
            creator);
        OperationalTask second = OperationalTask.CreateAssignedCopy(
            organizationId,
            distributionId,
            branchId,
            departmentId,
            secondUser,
            null,
            null,
            null,
            "Independent task",
            null,
            DateOnly.FromDateTime(start.UtcDateTime),
            start,
            start.AddHours(1),
            TaskPriority.Normal,
            false,
            creator);
        TaskItem firstItem = new(organizationId, first.Id, "Check", 0, true, EvidenceMode.None);
        TaskItem secondItem = new(organizationId, second.Id, "Check", 0, true, EvidenceMode.None);

        first.Start(firstUser, start);
        firstItem.Complete(firstUser, start.AddMinutes(5), false);
        first.Complete(firstUser, start.AddMinutes(10), true);

        Assert.Equal(distributionId, first.TaskDistributionId);
        Assert.Equal(distributionId, second.TaskDistributionId);
        Assert.Equal(OperationalTaskStatus.Completed, first.Status);
        Assert.Equal(TaskItemStatus.Completed, firstItem.Status);
        Assert.Equal(OperationalTaskStatus.NotStarted, second.Status);
        Assert.Equal(TaskItemStatus.Pending, secondItem.Status);
    }

    [Fact]
    public void Assigned_task_factory_rejects_an_empty_execution_owner()
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;

        Assert.Throws<DomainInvariantException>(() =>
            OperationalTask.CreateAssignedCopy(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.Empty,
                null,
                null,
                null,
                "Task",
                null,
                DateOnly.FromDateTime(start.UtcDateTime),
                start,
                start.AddHours(1),
                TaskPriority.Normal,
                false,
                Guid.NewGuid()));
    }

    [Fact]
    public void Distribution_has_assignment_metadata_but_no_execution_state()
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        TaskDistribution distribution = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            TaskAssignmentMode.SelectedUsers,
            DateOnly.FromDateTime(start.UtcDateTime),
            start,
            start.AddHours(1),
            Guid.NewGuid());
        string[] propertyNames = typeof(TaskDistribution).GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(TaskAssignmentMode.SelectedUsers, distribution.AssignmentMode);
        Assert.DoesNotContain("Status", propertyNames);
        Assert.DoesNotContain("AssigneeUserId", propertyNames);
    }

    [Fact]
    public void Template_item_activation_is_explicit_and_idempotent()
    {
        TaskTemplateItem item = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Template item",
            0);

        Assert.True(item.IsActive);

        item.Deactivate();
        item.Deactivate();
        Assert.False(item.IsActive);

        item.Activate();
        item.Activate();
        Assert.True(item.IsActive);
    }

    [Fact]
    public void Daily_schedule_accepts_no_recurrence_specific_fields()
    {
        TaskSchedule schedule = Schedule(RecurrenceType.Daily);

        Assert.Empty(schedule.Weekdays);
        Assert.Empty(schedule.MonthDays);
        Assert.False(schedule.IncludeLastDayOfMonth);
    }

    [Fact]
    public void Daily_schedule_rejects_month_days()
    {
        Assert.Throws<DomainInvariantException>(() =>
            Schedule(RecurrenceType.Daily, monthDays: [1]));
    }

    [Fact]
    public void Daily_schedule_rejects_include_last_day_of_month()
    {
        Assert.Throws<DomainInvariantException>(() =>
            Schedule(RecurrenceType.Daily, includeLastDayOfMonth: true));
    }

    [Fact]
    public void Weekly_schedule_requires_weekdays_and_daily_rejects_them()
    {
        Assert.Throws<DomainInvariantException>(() => Schedule(RecurrenceType.Weekly));
        Assert.Throws<DomainInvariantException>(() =>
            Schedule(RecurrenceType.Daily, weekdays: [Weekday.Monday]));
    }

    [Fact]
    public void Weekly_schedule_normalizes_duplicate_weekdays_in_deterministic_order()
    {
        TaskSchedule schedule = Schedule(
            RecurrenceType.Weekly,
            weekdays: [Weekday.Friday, Weekday.Monday, Weekday.Friday]);

        Assert.Equal([Weekday.Monday, Weekday.Friday], schedule.Weekdays);
    }

    [Fact]
    public void Weekly_schedule_rejects_month_days()
    {
        Assert.Throws<DomainInvariantException>(() =>
            Schedule(
                RecurrenceType.Weekly,
                weekdays: [Weekday.Monday],
                monthDays: [1]));
    }

    [Fact]
    public void Monthly_schedule_requires_days_or_last_day_flag()
    {
        Assert.Throws<DomainInvariantException>(() => Schedule(RecurrenceType.Monthly));
        Assert.Throws<DomainInvariantException>(() => Schedule(RecurrenceType.Monthly, monthDays: [32]));
        Assert.Throws<DomainInvariantException>(() =>
            Schedule(RecurrenceType.Monthly, weekdays: [Weekday.Monday], monthDays: [1]));
    }

    [Fact]
    public void Monthly_schedule_accepts_valid_month_days()
    {
        TaskSchedule schedule = Schedule(RecurrenceType.Monthly, monthDays: [15, 31]);

        Assert.Equal([15, 31], schedule.MonthDays);
        Assert.Empty(schedule.Weekdays);
    }

    [Fact]
    public void Monthly_schedule_accepts_include_last_day_of_month_only()
    {
        TaskSchedule schedule = Schedule(RecurrenceType.Monthly, includeLastDayOfMonth: true);

        Assert.True(schedule.IncludeLastDayOfMonth);
        Assert.Empty(schedule.MonthDays);
    }

    [Fact]
    public void Monthly_schedule_deduplicates_and_sorts_month_days()
    {
        TaskSchedule schedule = Schedule(RecurrenceType.Monthly, monthDays: [15, 5, 15, 28]);

        Assert.Equal([5, 15, 28], schedule.MonthDays);
    }

    [Fact]
    public void SpecificDates_recurrence_type_is_defined()
    {
        Assert.Contains("SpecificDates", Enum.GetNames<RecurrenceType>());
    }

    [Fact]
    public void SpecificDates_schedule_rejects_weekdays()
    {
        Assert.Throws<DomainInvariantException>(() =>
            Schedule(RecurrenceType.SpecificDates, weekdays: [Weekday.Monday]));
    }

    [Fact]
    public void SpecificDates_schedule_rejects_month_days()
    {
        Assert.Throws<DomainInvariantException>(() =>
            Schedule(RecurrenceType.SpecificDates, monthDays: [1]));
    }

    [Fact]
    public void Schedule_rejects_undefined_recurrence_and_weekday_values()
    {
        Assert.Throws<DomainInvariantException>(() => Schedule((RecurrenceType)99));
        Assert.Throws<DomainInvariantException>(() =>
            Schedule(RecurrenceType.Weekly, weekdays: [(Weekday)99]));
    }

    [Fact]
    public void Schedule_allows_equal_end_date_and_next_day_due_time()
    {
        DateOnly date = new(2026, 7, 27);
        TaskSchedule schedule = Schedule(
            RecurrenceType.Weekly,
            weekdays: [Weekday.Monday],
            startDate: date,
            endDate: date,
            startTime: new TimeOnly(22, 0),
            dueTime: new TimeOnly(2, 0),
            dueDayOffset: 1);

        Assert.Equal(date, schedule.RecurrenceEndDate);
        Assert.Equal(1, schedule.ExecutionDueDayOffset);
    }

    [Fact]
    public void Overdue_is_derived_and_terminal_tasks_are_not_overdue()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        OperationalTask task = CreateTask(dueAt: now.AddMinutes(-1));

        Assert.True(task.IsOverdue(now));

        task.Start(Guid.NewGuid(), now.AddMinutes(-10));
        task.Complete(Guid.NewGuid(), now, true);

        Assert.False(task.IsOverdue(now.AddMinutes(1)));
    }

    [Fact]
    public void Task_template_allows_no_default_department()
    {
        TaskTemplate template = new(Guid.NewGuid(), null, "General task", Guid.NewGuid());

        Assert.Null(template.DefaultDepartmentId);
    }

    [Fact]
    public void Status_history_preserves_business_time_separately_from_audit_creation_time()
    {
        DateTimeOffset occurredAt = new(2026, 7, 20, 12, 30, 0, TimeSpan.Zero);
        TaskTransition transition = new(
            OperationalTaskStatus.NotStarted,
            OperationalTaskStatus.InProgress,
            Guid.NewGuid(),
            occurredAt,
            "Started");

        TaskStatusHistory history = TaskStatusHistory.FromTransition(
            Guid.NewGuid(),
            Guid.NewGuid(),
            transition);
        TaskStatusHistory created = TaskStatusHistory.Created(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            occurredAt);

        Assert.Equal(occurredAt, history.OccurredAt);
        Assert.Equal(occurredAt, created.OccurredAt);
        Assert.Equal(default, history.CreatedAt);
        Assert.Equal(default, created.CreatedAt);
    }

    [Fact]
    public void Status_history_rejects_default_business_time()
    {
        Assert.Throws<DomainInvariantException>(() =>
            TaskStatusHistory.Created(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                default));
    }

    [Fact]
    public void Department_order_cannot_be_received_before_delivery()
    {
        DepartmentOrder order = CreateOrder();

        Assert.Throws<DomainInvariantException>(() =>
            order.ConfirmReceipt(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Department_order_item_rejects_negative_quantities()
    {
        DepartmentOrderItem item = new()
        {
            ItemNameSnapshot = "Snapshot item",
            UnitCodeSnapshot = UnitCode.Each,
            RequestedQuantity = -1,
        };

        Assert.Throws<DomainInvariantException>(item.ValidateQuantities);
    }

    private static OperationalTask CreateTask(bool requiresApproval = false, DateTimeOffset? dueAt = null)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow.AddHours(-2);
        OperationalTask task = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test task",
            DateOnly.FromDateTime(start.UtcDateTime),
            start,
            dueAt ?? start.AddHours(1),
            Guid.NewGuid());
        task.Configure(null, null, null, null, null, TaskPriority.Normal, requiresApproval);
        return task;
    }

    private static TaskItem CreateItem(bool isRequired, EvidenceMode evidenceMode) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Checklist item",
            0,
            isRequired,
            evidenceMode);
    [Fact]
    public void Task_execution_window_state_evaluates_not_open_open_and_expired()
    {
        DateTimeOffset start = new(2026, 7, 29, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset due = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        OperationalTask task = new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Window Test", DateOnly.FromDateTime(start.Date),
            start, due, Guid.NewGuid());

        // Before ScheduledStartAt
        Assert.Equal(TaskExecutionWindowState.NotOpen, task.GetExecutionWindowState(start.AddSeconds(-1)));
        Assert.False(task.CanStartInWindow(start.AddSeconds(-1)));

        // Exactly at ScheduledStartAt
        Assert.Equal(TaskExecutionWindowState.Open, task.GetExecutionWindowState(start));
        Assert.True(task.CanStartInWindow(start));

        // Inside window
        Assert.Equal(TaskExecutionWindowState.Open, task.GetExecutionWindowState(start.AddHours(1)));
        Assert.True(task.CanStartInWindow(start.AddHours(1)));

        // Exactly at DueAt
        Assert.Equal(TaskExecutionWindowState.Open, task.GetExecutionWindowState(due));
        Assert.True(task.CanStartInWindow(due));

        // After DueAt
        Assert.Equal(TaskExecutionWindowState.Expired, task.GetExecutionWindowState(due.AddSeconds(1)));
        Assert.False(task.CanStartInWindow(due.AddSeconds(1)));
    }

    private static TaskSchedule Schedule(
        RecurrenceType recurrence,
        IReadOnlyCollection<Weekday>? weekdays = null,
        IEnumerable<int>? monthDays = null,
        bool includeLastDayOfMonth = false,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        TimeOnly? startTime = null,
        TimeOnly? dueTime = null,
        int dueDayOffset = 0) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            TaskAssignmentMode.SingleUser,
            recurrence,
            startDate ?? new DateOnly(2026, 7, 27),
            startTime ?? new TimeOnly(9, 0),
            dueTime ?? new TimeOnly(10, 0),
            Guid.NewGuid(),
            weekdays,
            monthDays,
            includeLastDayOfMonth,
            endDate,
            dueDayOffset);

    private static DepartmentOrder CreateOrder() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "ORD-1",
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);
}
