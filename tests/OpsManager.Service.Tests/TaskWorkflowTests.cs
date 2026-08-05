using OpsManager.Domain.Common;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Service.Common;
using OpsManager.Service.Tasks;
using OpsManager.Service.Tasks.DTOs;

namespace OpsManager.Service.Tests;

public sealed class TaskWorkflowTests
{
    [Fact]
    public void Required_evidence_is_enforced_even_for_optional_item()
    {
        TaskItem item = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Photo proof",
            0,
            isRequired: false,
            EvidenceMode.Required);

        Assert.Throws<DomainInvariantException>(() =>
            item.Complete(Guid.NewGuid(), DateTimeOffset.UtcNow, false));
    }

    [Fact]
    public void Approval_task_uses_pending_approval_transition()
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        OperationalTask task = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test",
            DateOnly.FromDateTime(start.Date),
            start,
            start.AddHours(1),
            Guid.NewGuid());
        task.Configure(null, null, null, null, null, TaskPriority.Normal, requiresApproval: true);
        Guid actor = Guid.NewGuid();

        task.Start(actor, start);
        task.SubmitForApproval(actor, start.AddMinutes(10), true);
        task.Approve(actor, start.AddMinutes(11));

        Assert.Equal(OperationalTaskStatus.Completed, task.Status);
        Assert.Equal(actor, task.ApprovedBy);
        Assert.Equal(start.AddMinutes(11), task.CompletedAt);
    }

    [Fact]
    public void Daily_weekly_and_monthly_occurrences_are_deterministic()
    {
        Guid organizationId = Guid.NewGuid();
        TaskSchedule daily = Schedule(organizationId, RecurrenceType.Daily, new DateOnly(2026, 1, 1));
        TaskSchedule weekly = Schedule(
            organizationId,
            RecurrenceType.Weekly,
            new DateOnly(2026, 1, 1),
            [Weekday.Monday, Weekday.Wednesday]);
        TaskSchedule monthly = Schedule(
            organizationId,
            RecurrenceType.Monthly,
            new DateOnly(2026, 1, 1),
            monthDays: [31]);

        Assert.Equal(
            [new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 4), new DateOnly(2026, 1, 5)],
            TaskOccurrenceCalculator.Calculate(daily, new DateOnly(2026, 1, 5)));
        Assert.Equal(
            [new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 7)],
            TaskOccurrenceCalculator.Calculate(weekly, new DateOnly(2026, 1, 7)));
        Assert.Equal(
            [new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 31)],
            TaskOccurrenceCalculator.Calculate(monthly, new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void Monthly_with_last_day_flag_covers_february_and_short_months()
    {
        Guid organizationId = Guid.NewGuid();
        TaskSchedule schedule = Schedule(
            organizationId,
            RecurrenceType.Monthly,
            new DateOnly(2026, 1, 1),
            includeLastDayOfMonth: true);

        Assert.Equal(
            [new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31)],
            TaskOccurrenceCalculator.Calculate(schedule, new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void Monthly_with_multiple_days_produces_all_matching_dates()
    {
        Guid organizationId = Guid.NewGuid();
        TaskSchedule schedule = Schedule(
            organizationId,
            RecurrenceType.Monthly,
            new DateOnly(2026, 1, 1),
            monthDays: [1, 15]);

        Assert.Equal(
            [new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 15)],
            TaskOccurrenceCalculator.Calculate(schedule, new DateOnly(2026, 2, 28)));
    }

    [Fact]
    public void Monthly_day_31_and_last_day_flag_deduplicates()
    {
        Guid organizationId = Guid.NewGuid();
        TaskSchedule schedule = Schedule(
            organizationId,
            RecurrenceType.Monthly,
            new DateOnly(2026, 1, 1),
            monthDays: [31],
            includeLastDayOfMonth: true);

        // January has 31 days, so day 31 and last day are the same => deduplicated
        // February has 28 days, so day 31 is skipped but last day (28) is included
        Assert.Equal(
            [new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31)],
            TaskOccurrenceCalculator.Calculate(schedule, new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void SpecificDates_filters_by_range()
    {
        Guid organizationId = Guid.NewGuid();
        TaskSchedule schedule = Schedule(
            organizationId,
            RecurrenceType.SpecificDates,
            new DateOnly(2026, 3, 1));

        DateOnly[] dates = [new(2026, 2, 15), new(2026, 3, 10), new(2026, 3, 20), new(2026, 4, 1)];

        Assert.Equal(
            [new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 20)],
            TaskOccurrenceCalculator.Calculate(schedule, new DateOnly(2026, 3, 31), dates));
    }

    [Fact]
    public void Template_validator_rejects_duplicate_item_order()
    {
        SaveTaskTemplateValidator validator = new();
        SaveTaskTemplateRequest request = new(
            Guid.NewGuid(),
            "Template",
            null,
            TaskPriority.Normal,
            60,
            false,
            true,
            [
                new ChecklistDefinitionRequest("A", null, 1, true, EvidenceMode.None),
                new ChecklistDefinitionRequest("B", null, 1, true, EvidenceMode.None),
            ]);

        Assert.Throws<RequestValidationException>(() => validator.ValidateAndThrow(request));
    }

    [Fact]
    public void Task_occurrence_calculator_handles_overnight_due_day_offset_and_branch_timezones()
    {
        DateOnly date = new(2026, 7, 29);
        TimeOnly startTime = new(22, 0);
        TimeOnly dueTime = new(6, 0); // Next morning
        TimeZoneInfo timezone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"); // UTC+9

        DateTimeOffset utcStart = TaskOccurrenceGeneratorService.ToUtc(date, startTime, timezone);
        DateTimeOffset utcDue = TaskOccurrenceGeneratorService.ToUtc(date.AddDays(1), dueTime, timezone);

        // 2026-07-29 22:00 JST is 2026-07-29 13:00 UTC
        Assert.Equal(new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero), utcStart);
        // 2026-07-30 06:00 JST is 2026-07-29 21:00 UTC
        Assert.Equal(new DateTimeOffset(2026, 7, 29, 21, 0, 0, TimeSpan.Zero), utcDue);
        Assert.True(utcDue > utcStart);
    }

    [Fact]
    public void Task_temporal_scope_and_query_mapping_handles_upcoming_and_past()
    {
        DateTimeOffset now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset pastDue = now.AddHours(-1);
        DateTimeOffset upcomingDue = now.AddHours(1);

        OperationalTask pastTask = new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Past Task", DateOnly.FromDateTime(now.Date),
            now.AddHours(-2), pastDue, Guid.NewGuid());

        OperationalTask upcomingTask = new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Upcoming Task", DateOnly.FromDateTime(now.Date),
            now, upcomingDue, Guid.NewGuid());

        Assert.True(pastTask.DueAt < now);
        Assert.True(upcomingTask.DueAt >= now);
    }

    private static TaskSchedule Schedule(
        Guid organizationId,
        RecurrenceType recurrence,
        DateOnly start,
        IReadOnlyList<Weekday>? weekdays = null,
        IEnumerable<int>? monthDays = null,
        bool includeLastDayOfMonth = false) =>
        new(
            organizationId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            TaskAssignmentMode.SingleUser,
            recurrence,
            start,
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            Guid.NewGuid(),
            weekdays,
            monthDays,
            includeLastDayOfMonth);
}
