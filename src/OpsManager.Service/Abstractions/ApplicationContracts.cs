using OpsManager.Domain.Enums;

namespace OpsManager.Service.Abstractions;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? PlatformUserId { get; }
    Guid? OrganizationId { get; }
    OrganizationRole? OrganizationRole { get; }
    PlatformRole? PlatformRole { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}

public interface ICurrentTenantContext
{
    Guid? OrganizationId { get; }
}

public interface IAuthenticationTenantScope
{
    IDisposable Begin(Guid organizationId);
    IDisposable BeginBypass();
}

public interface IPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string passwordHash, string providedPassword);
}

public sealed record AccessTokenDescriptor(
    Guid SubjectId,
    string Email,
    DateTimeOffset IssuedAt,
    Guid? OrganizationId = null,
    OrganizationRole? OrganizationRole = null,
    PlatformRole? PlatformRole = null);

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    IssuedAccessToken CreateAccessToken(AccessTokenDescriptor descriptor);
    string GenerateRefreshToken();
    string HashRefreshToken(string token);
    TimeSpan RefreshTokenLifetime { get; }
}

public sealed record StoredFile(string Url, string FileName, string ContentType, long Length);

public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string url, CancellationToken cancellationToken = default);

    string ResolveUrl(string? storagePathOrKey);
}

public interface INotificationService
{
    Task CreateAsync(
        Guid organizationId,
        Guid userId,
        NotificationType type,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? parameters = null,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        CancellationToken cancellationToken = default);
}

public interface IAuditService
{
    Task RecordTenantAsync(
        Guid organizationId,
        string action,
        string entityType,
        Guid? entityId,
        IReadOnlyDictionary<string, string>? oldValues = null,
        IReadOnlyDictionary<string, string>? newValues = null,
        CancellationToken cancellationToken = default);

    Task RecordPlatformAsync(
        string action,
        string entityType,
        Guid? entityId,
        Guid? organizationId = null,
        IReadOnlyDictionary<string, string>? oldValues = null,
        IReadOnlyDictionary<string, string>? newValues = null,
        CancellationToken cancellationToken = default);
}

public enum SubscriptionAccessMode
{
    Full,
    GraceLimited,
    ReadOnly,
    Blocked,
}

public sealed record SubscriptionAccess(
    SubscriptionAccessMode Mode,
    SubscriptionStatus? Status,
    DateTimeOffset? ExpiresAt,
    string? Reason);

public interface ISubscriptionAccessService
{
    Task<SubscriptionAccess> GetAccessAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task EnsureReadAllowedAsync(Guid organizationId, string? featureKey = null, CancellationToken cancellationToken = default);
    Task EnsureWriteAllowedAsync(Guid organizationId, string? featureKey = null, CancellationToken cancellationToken = default);
}
