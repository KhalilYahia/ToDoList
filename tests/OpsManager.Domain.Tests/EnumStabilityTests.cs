using OpsManager.Domain.Enums;

namespace OpsManager.Domain.Tests;

public sealed class EnumStabilityTests
{
    [Fact]
    public void Organization_roles_have_stable_string_codes()
    {
        Assert.Equal(["Manager", "Supervisor", "Employee"], Enum.GetNames<OrganizationRole>());
    }

    [Fact]
    public void Task_statuses_have_stable_string_codes()
    {
        Assert.Equal(
            ["NotStarted", "InProgress", "Blocked", "PendingApproval", "Returned", "Completed", "Cancelled"],
            Enum.GetNames<OperationalTaskStatus>());
    }

    [Fact]
    public void Weekdays_align_with_system_day_of_week_values()
    {
        Assert.Equal(
            ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"],
            Enum.GetNames<Weekday>());
    }

    [Fact]
    public void Task_assignment_modes_have_stable_wire_codes()
    {
        Assert.Equal(
            ["SingleUser", "SelectedUsers", "AllDepartmentMembers"],
            Enum.GetNames<TaskAssignmentMode>());
    }

    [Fact]
    public void Subscription_statuses_have_stable_string_codes()
    {
        Assert.Equal(
            ["Trial", "Active", "GracePeriod", "Expired", "Suspended", "Cancelled", "Complimentary"],
            Enum.GetNames<SubscriptionStatus>());
    }
}
