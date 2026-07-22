using OpsManager.Domain.Common;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using TaskEntity = OpsManager.Domain.Entities.Task;
using TaskStatus = OpsManager.Domain.Enums.TaskStatus;

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
    public void Task_rejects_due_time_that_is_not_after_start()
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;

        Assert.Throws<DomainInvariantException>(() => new TaskEntity(
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
    public void Task_rejects_invalid_state_transition()
    {
        TaskEntity task = CreateTask();

        Assert.Throws<InvalidStateTransitionException>(() =>
            task.TransitionTo(TaskStatus.Approved, Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Task_requiring_approval_cannot_complete_directly()
    {
        TaskEntity task = CreateTask();
        task.RequiresApproval = true;
        task.TransitionTo(TaskStatus.InProgress, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainInvariantException>(() =>
            task.TransitionTo(TaskStatus.Completed, Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Required_task_item_with_required_evidence_cannot_complete_without_attachment()
    {
        TaskItem item = new()
        {
            IsRequired = true,
            EvidenceMode = EvidenceMode.Required,
            Title = "Photo proof",
        };

        Assert.Throws<DomainInvariantException>(() =>
            item.Complete(Guid.NewGuid(), DateTimeOffset.UtcNow, hasEvidenceAttachment: false));
    }

    [Fact]
    public void Weekly_schedule_requires_at_least_one_valid_weekday()
    {
        Assert.Throws<DomainInvariantException>(() => new TaskSchedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            RecurrenceType.Weekly,
            1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new TimeOnly(9, 0),
            new TimeOnly(10, 0)));
    }

    [Fact]
    public void Monthly_schedule_requires_valid_month_day()
    {
        Assert.Throws<DomainInvariantException>(() => new TaskSchedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            RecurrenceType.Monthly,
            1,
            DateOnly.FromDateTime(DateTime.UtcNow),
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            monthDay: 32));
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

    private static TaskEntity CreateTask()
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        return new TaskEntity(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test task",
            DateOnly.FromDateTime(start.UtcDateTime),
            start,
            start.AddHours(1),
            Guid.NewGuid());
    }

    private static DepartmentOrder CreateOrder() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "ORD-1",
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);
}
