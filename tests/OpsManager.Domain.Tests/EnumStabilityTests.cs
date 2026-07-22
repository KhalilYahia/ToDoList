using OpsManager.Domain.Enums;
using TaskStatus = OpsManager.Domain.Enums.TaskStatus;

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
            ["Pending", "InProgress", "Blocked", "Completed", "AwaitingApproval", "Approved", "Rejected", "Cancelled"],
            Enum.GetNames<TaskStatus>());
    }

    [Fact]
    public void Subscription_statuses_have_stable_string_codes()
    {
        Assert.Equal(
            ["Trial", "Active", "GracePeriod", "Expired", "Suspended", "Cancelled"],
            Enum.GetNames<SubscriptionStatus>());
    }
}
