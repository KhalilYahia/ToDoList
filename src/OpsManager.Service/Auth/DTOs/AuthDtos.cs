using OpsManager.Domain.Enums;
using OpsManager.Service.Abstractions;

namespace OpsManager.Service.Auth.DTOs;

public sealed record RegisterOrganizationRequest(
    string OrganizationName,
    string? LegalName,
    string Timezone,
    string DefaultLanguage,
    string ManagerFullName,
    string ManagerEmail,
    string Password,
    string? Phone);

public sealed record LoginRequest(Guid? OrganizationId, string Email, string Password);

public sealed record OrganizationSummaryDto(
    Guid Id,
    string Name,
    string? LegalName,
    string Timezone,
    string DefaultLanguage,
    OrganizationStatus Status);

public sealed record CurrentUserDto(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    string PreferredLanguage,
    UserAccountStatus AccountStatus);

public sealed record MembershipDto(Guid Id, OrganizationRole Role, bool IsActive, DateTimeOffset JoinedAt);

public sealed record AuthenticationResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    CurrentUserDto User,
    OrganizationSummaryDto Organization,
    MembershipDto Membership,
    SubscriptionAccess Access);

public sealed record AuthenticationSession(AuthenticationResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

public sealed record MeResponse(
    CurrentUserDto User,
    OrganizationSummaryDto Organization,
    MembershipDto Membership,
    IReadOnlyList<Guid> DepartmentIds,
    SubscriptionAccess Access);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
