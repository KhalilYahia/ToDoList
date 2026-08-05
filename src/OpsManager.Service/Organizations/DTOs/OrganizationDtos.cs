using OpsManager.Domain.Enums;

namespace OpsManager.Service.Organizations.DTOs;

public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string? LegalName,
    string? LogoUrl,
    string? Phone,
    string? Email,
    string Timezone,
    string DefaultLanguage,
    OrganizationStatus Status);

public sealed record UpdateOrganizationRequest(
    string Name,
    string? LegalName,
    string? LogoUrl,
    string? Phone,
    string? Email,
    string Timezone,
    string DefaultLanguage);

public sealed record BranchDto(
    Guid Id,
    string Name,
    string? Address,
    string? Phone,
    string Timezone,
    bool IsPrimary,
    bool IsActive);

public sealed record SaveBranchRequest(
    string Name,
    string? Address,
    string? Phone,
    string Timezone,
    bool IsPrimary,
    bool IsActive = true);

public sealed record DepartmentDto(
    Guid Id,
    Guid BranchId,
    string Name,
    string? Description,
    Guid? SupervisorUserId,
    bool IsActive);

public sealed record SaveDepartmentRequest(
    Guid BranchId,
    string Name,
    string? Description,
    Guid? SupervisorUserId,
    bool IsActive = true);

public sealed record MemberDto(
    Guid MembershipId,
    Guid UserId,
    string FullName,
    string? Email,
    string? Phone,
    OrganizationRole Role,
    bool IsActive,
    UserAccountStatus AccountStatus,
    bool MustChangePassword,
    IReadOnlyList<Guid> DepartmentIds);

public sealed record CreateMemberRequest(
    string FullName,
    string Email,
    string? Phone,
    string PreferredLanguage,
    OrganizationRole Role,
    string TemporaryPassword,
    IReadOnlyList<Guid> DepartmentIds);

public sealed record UpdateMemberRequest(
    string FullName,
    string? Phone,
    string PreferredLanguage,
    OrganizationRole Role);

public sealed record SetMemberDepartmentsRequest(IReadOnlyList<Guid> DepartmentIds);

public sealed record ResetMemberPasswordRequest(string NewPassword);
