using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Infrastructure;

public interface IOperationalNotificationDispatchService
{
    Task<int> DispatchAsync(CancellationToken cancellationToken = default);
}

public sealed class OperationalNotificationDispatchService(
    IUnitOfWork unitOfWork,
    IAuthenticationTenantScope tenantScope,
    INotificationService notifications,
    IClock clock) : IOperationalNotificationDispatchService
{
    public async Task<int> DispatchAsync(CancellationToken cancellationToken = default)
    {
        using IDisposable bypass = tenantScope.BeginBypass();
        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset dueSoon = now.AddHours(24);
        IReadOnlyList<TaskAlert> taskAlerts = await unitOfWork.Repository<OperationalTask>().ProjectAsync(
            task => task.AssigneeUserId.HasValue &&
                task.DueAt <= dueSoon &&
                task.Status != OperationalTaskStatus.Completed &&
                task.Status != OperationalTaskStatus.Cancelled,
            task => new TaskAlert(
                task.Id,
                task.OrganizationId,
                task.AssigneeUserId!.Value,
                task.Title,
                task.DueAt),
            cancellationToken);
        int created = 0;
        foreach (TaskAlert alert in taskAlerts)
        {
            NotificationType type = alert.DueAt < now ? NotificationType.System : NotificationType.TaskDue;
            bool exists = await unitOfWork.Repository<Notification>().AnyAsync(
                notification => notification.OrganizationId == alert.OrganizationId &&
                    notification.UserId == alert.UserId &&
                    notification.NotificationType == type &&
                    notification.RelatedEntityType == nameof(OperationalTask) &&
                    notification.RelatedEntityId == alert.TaskId,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            using IDisposable tenant = tenantScope.Begin(alert.OrganizationId);
            await notifications.CreateAsync(
                alert.OrganizationId,
                alert.UserId,
                type,
                alert.DueAt < now ? "Task overdue" : "Task due soon",
                alert.Title,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["dueAt"] = alert.DueAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                },
                nameof(OperationalTask),
                alert.TaskId,
                cancellationToken);
            created++;
        }

        IReadOnlyList<SubscriptionAlert> subscriptionAlerts =
            await unitOfWork.Repository<OrganizationSubscription>().ProjectAsync(
                subscription =>
                    subscription.Status == SubscriptionStatus.Expired ||
                    subscription.Status == SubscriptionStatus.Trial && subscription.TrialEndsAt <= now.AddDays(7) ||
                    (subscription.Status == SubscriptionStatus.Active ||
                        subscription.Status == SubscriptionStatus.Complimentary) &&
                    subscription.EndsAt <= now.AddDays(7),
                subscription => new SubscriptionAlert(
                    subscription.Id,
                    subscription.OrganizationId,
                    subscription.Status,
                    subscription.EndsAt ?? subscription.TrialEndsAt),
                cancellationToken);
        foreach (SubscriptionAlert alert in subscriptionAlerts)
        {
            IReadOnlyList<Guid> managers = await unitOfWork.Repository<OrganizationMember>().ProjectAsync(
                member => member.OrganizationId == alert.OrganizationId &&
                    member.IsActive &&
                    member.Role == OrganizationRole.Manager,
                member => member.UserId,
                cancellationToken);
            foreach (Guid managerId in managers)
            {
                NotificationType type = alert.Status == SubscriptionStatus.Expired
                    ? NotificationType.System
                    : NotificationType.SubscriptionUpdated;
                bool exists = await unitOfWork.Repository<Notification>().AnyAsync(
                    notification => notification.OrganizationId == alert.OrganizationId &&
                        notification.UserId == managerId &&
                        notification.NotificationType == type &&
                        notification.RelatedEntityType == nameof(OrganizationSubscription) &&
                        notification.RelatedEntityId == alert.SubscriptionId,
                    cancellationToken);
                if (exists)
                {
                    continue;
                }

                using IDisposable tenant = tenantScope.Begin(alert.OrganizationId);
                await notifications.CreateAsync(
                    alert.OrganizationId,
                    managerId,
                    type,
                    alert.Status == SubscriptionStatus.Expired ? "Subscription expired" : "Subscription expiring",
                    alert.Status.ToString(),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["expiresAt"] = alert.ExpiresAt?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    },
                    nameof(OrganizationSubscription),
                    alert.SubscriptionId,
                    cancellationToken);
                created++;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created;
    }

    private sealed record TaskAlert(
        Guid TaskId,
        Guid OrganizationId,
        Guid UserId,
        string Title,
        DateTimeOffset DueAt);

    private sealed record SubscriptionAlert(
        Guid SubscriptionId,
        Guid OrganizationId,
        SubscriptionStatus Status,
        DateTimeOffset? ExpiresAt);
}
