using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsManager.Domain.Constants;
using OpsManager.Service.Common;
using OpsManager.Service.Notifications;
using OpsManager.Service.Notifications.DTOs;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize(Policy = PolicyNames.OrganizationMember)]
public sealed class NotificationsController(IUserNotificationService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<NotificationDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        service.ListAsync(new PageQuery(page, pageSize), cancellationToken);

    [HttpPost("{id:guid}/read")]
    public Task MarkRead(Guid id, CancellationToken cancellationToken) =>
        service.MarkReadAsync(id, cancellationToken);

    [HttpPost("read-all")]
    public Task MarkAllRead(CancellationToken cancellationToken) =>
        service.MarkAllReadAsync(cancellationToken);

    [HttpGet("unread-count")]
    public Task<UnreadNotificationCountDto> UnreadCount(CancellationToken cancellationToken) =>
        service.GetUnreadCountAsync(cancellationToken);
}
