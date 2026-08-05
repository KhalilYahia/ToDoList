using OpsManager.Domain.Enums;

namespace OpsManager.Service.Notifications.DTOs;

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Parameters,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);

public sealed record UnreadNotificationCountDto(int Count);
