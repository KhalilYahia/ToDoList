using OpsManager.Domain.Common;
using OpsManager.Domain.Constants;
using OpsManager.Domain.Enums;

namespace OpsManager.Domain.Entities;

public sealed class Organization : SoftDeletableEntity
{
    private Organization() { }

    public Organization(string name, string timezone, string defaultLanguage)
    {
        Name = Guard.Required(name, nameof(name), 200);
        Timezone = Guard.Required(timezone, nameof(timezone), 100);
        Guard.SupportedLanguage(defaultLanguage, nameof(defaultLanguage));
        DefaultLanguage = defaultLanguage;
    }

    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? LogoUrl { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string DefaultLanguage { get; set; } = SupportedLanguages.English;
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;
    public Guid? CreatedBy { get; set; }

    public void ChangeDefaultLanguage(string language)
    {
        Guard.SupportedLanguage(language, nameof(language));
        DefaultLanguage = language;
    }
}

public sealed class Branch : TenantSoftDeletableEntity
{
    private Branch() { }

    public Branch(Guid organizationId, string name, string timezone)
    {
        OrganizationId = organizationId;
        Name = Guard.Required(name, nameof(name), 160);
        Timezone = Guard.Required(timezone, nameof(timezone), 100);
    }

    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string Timezone { get; set; } = "UTC";
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Department : TenantSoftDeletableEntity
{
    private Department() { }

    public Department(Guid organizationId, Guid branchId, string name)
    {
        OrganizationId = organizationId;
        BranchId = branchId;
        Name = Guard.Required(name, nameof(name), 160);
    }

    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? SupervisorUserId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class User : SoftDeletableEntity
{
    private User() { }

    public User(string fullName, string email, string passwordHash, string preferredLanguage)
    {
        FullName = Guard.Required(fullName, nameof(fullName), 200);
        Email = Guard.Required(email, nameof(email), 320);
        NormalizedEmail = Email.ToUpperInvariant();
        PasswordHash = Guard.Required(passwordHash, nameof(passwordHash), 1000);
        Guard.SupportedLanguage(preferredLanguage, nameof(preferredLanguage));
        PreferredLanguage = preferredLanguage;
    }

    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string PreferredLanguage { get; set; } = SupportedLanguages.English;
    public UserAccountStatus AccountStatus { get; set; } = UserAccountStatus.Active;
    public bool MustChangePassword { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public sealed class RefreshToken : BaseEntity
{
    private RefreshToken() { }

    public RefreshToken(
        Guid? userId,
        Guid? platformUserId,
        Guid? organizationId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset expiresAt)
    {
        if ((userId is null) == (platformUserId is null))
        {
            throw new DomainInvariantException("A refresh token must belong to exactly one user type.");
        }

        if (userId is not null && organizationId is null)
        {
            throw new DomainInvariantException("An organization refresh token requires an organization.");
        }

        UserId = userId;
        PlatformUserId = platformUserId;
        OrganizationId = organizationId;
        FamilyId = familyId;
        TokenHash = Guard.Required(tokenHash, nameof(tokenHash), 512);
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid? UserId { get; set; }
    public Guid? PlatformUserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
    public string? RevocationReason { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset revokedAt, string? revokedByIp, Guid? replacementId, string? reason = null)
    {
        if (RevokedAt is not null)
        {
            throw new DomainInvariantException("Refresh token has already been revoked.");
        }

        RevokedAt = revokedAt;
        RevokedByIp = Guard.Optional(revokedByIp, 64);
        ReplacedByTokenId = replacementId;
        RevocationReason = Guard.Optional(reason, 240);
    }
}

public sealed class OrganizationMember : TenantAuditableEntity
{
    private OrganizationMember() { }

    public OrganizationMember(Guid organizationId, Guid userId, OrganizationRole role, DateTimeOffset joinedAt)
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid UserId { get; set; }
    public OrganizationRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
}

public sealed class UserDepartment : TenantAuditableEntity
{
    private UserDepartment() { }

    public UserDepartment(Guid organizationId, Guid userId, Guid departmentId, DateTimeOffset joinedAt)
    {
        OrganizationId = organizationId;
        UserId = userId;
        DepartmentId = departmentId;
        JoinedAt = joinedAt;
    }

    public Guid UserId { get; set; }
    public Guid DepartmentId { get; set; }
    public bool IsPrimary { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
}
