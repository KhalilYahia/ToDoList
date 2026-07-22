namespace OpsManager.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }

    DateTimeOffset UpdatedAt { get; set; }
}

public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }
}

public interface ITenantEntity
{
    Guid OrganizationId { get; set; }
}

public abstract class AuditableEntity : BaseEntity, IAuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public abstract class SoftDeletableEntity : AuditableEntity, ISoftDeletable
{
    public DateTimeOffset? DeletedAt { get; set; }
}

public abstract class TenantAuditableEntity : AuditableEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
}

public abstract class TenantSoftDeletableEntity : TenantAuditableEntity, ISoftDeletable
{
    public DateTimeOffset? DeletedAt { get; set; }
}
