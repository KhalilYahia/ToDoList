using OpsManager.Domain.Entities;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Notifications.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Notifications;

public interface IUserNotificationService
{
    Task<PagedResponse<NotificationDto>> ListAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(CancellationToken cancellationToken = default);
    Task<UnreadNotificationCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default);
}

public sealed class UserNotificationService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    IClock clock) : IUserNotificationService
{
    public async Task<PagedResponse<NotificationDto>> ListAsync(
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        Guid userId = currentUser.UserId!.Value;
        PagedResult<Notification> result = await unitOfWork.Repository<Notification>().ListAsync(
            notification => notification.OrganizationId == organizationId && notification.UserId == userId,
            page.ToDomain(),
            cancellationToken);
        return PagedResponse.Map(result, Map);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guid userId = AuthorizationGuard.RequireUser(currentUser);
        Notification notification = await unitOfWork.Repository<Notification>().FirstOrDefaultAsync(
            item => item.Id == id && item.UserId == userId,
            cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Notification));
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = clock.UtcNow;
            unitOfWork.Repository<Notification>().Update(notification);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        Guid userId = AuthorizationGuard.RequireUser(currentUser);
        PagedResult<Notification> page = await unitOfWork.Repository<Notification>().ListAsync(
            item => item.UserId == userId && !item.IsRead,
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);
        while (page.Items.Count > 0)
        {
            foreach (Notification notification in page.Items)
            {
                notification.IsRead = true;
                notification.ReadAt = clock.UtcNow;
                unitOfWork.Repository<Notification>().Update(notification);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            page = await unitOfWork.Repository<Notification>().ListAsync(
                item => item.UserId == userId && !item.IsRead,
                new PageRequest(1, PageRequest.MaximumPageSize),
                cancellationToken);
        }
    }

    public async Task<UnreadNotificationCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        Guid userId = AuthorizationGuard.RequireUser(currentUser);
        int count = await unitOfWork.Repository<Notification>()
            .CountAsync(item => item.UserId == userId && !item.IsRead, cancellationToken);
        return new UnreadNotificationCountDto(count);
    }

    private static NotificationDto Map(Notification notification) =>
        new(
            notification.Id,
            notification.NotificationType,
            notification.Title,
            notification.Body,
            new Dictionary<string, string>(notification.Parameters, StringComparer.Ordinal),
            notification.RelatedEntityType,
            notification.RelatedEntityId,
            notification.IsRead,
            notification.ReadAt,
            notification.CreatedAt);
}
