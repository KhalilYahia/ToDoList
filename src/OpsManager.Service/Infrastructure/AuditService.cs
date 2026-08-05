using OpsManager.Domain.Entities;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Infrastructure;

public sealed class AuditService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    IClock clock) : IAuditService
{
    public Task RecordTenantAsync(
        Guid organizationId,
        string action,
        string entityType,
        Guid? entityId,
        IReadOnlyDictionary<string, string>? oldValues = null,
        IReadOnlyDictionary<string, string>? newValues = null,
        CancellationToken cancellationToken = default) =>
        unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            OrganizationId = organizationId,
            ActorUserId = currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = Copy(oldValues),
            NewValues = Copy(newValues),
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent,
            CreatedAt = clock.UtcNow,
        }, cancellationToken);

    public Task RecordPlatformAsync(
        string action,
        string entityType,
        Guid? entityId,
        Guid? organizationId = null,
        IReadOnlyDictionary<string, string>? oldValues = null,
        IReadOnlyDictionary<string, string>? newValues = null,
        CancellationToken cancellationToken = default) =>
        unitOfWork.Repository<PlatformAuditLog>().AddAsync(new PlatformAuditLog
        {
            ActorPlatformUserId = currentUser.PlatformUserId,
            OrganizationId = organizationId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = Copy(oldValues),
            NewValues = Copy(newValues),
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent,
            CreatedAt = clock.UtcNow,
        }, cancellationToken);

    private static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string>? values) =>
        values is null ? new(StringComparer.Ordinal) : new(values, StringComparer.Ordinal);
}
