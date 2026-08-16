using OpsManager.Domain.Constants;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Auth.DTOs;
using OpsManager.Service.Common;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Auth;

public sealed record OnboardingOptions(string DefaultPlanCode, int TrialDays, string PrimaryBranchName);

public interface IAuthService
{
    Task<AuthenticationSession> RegisterOrganizationAsync(RegisterOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<AuthenticationSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthenticationSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<MeResponse> GetMeAsync(CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<CurrentUserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<StoredFile> UploadMyAvatarAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
}

public sealed class AuthService(
    IUnitOfWork unitOfWork,
    IPasswordService passwordService,
    ITokenService tokenService,
    IClock clock,
    IAuthenticationTenantScope authenticationTenantScope,
    ICurrentUserContext currentUser,
    ISubscriptionAccessService subscriptionAccessService,
    IAuditService auditService,
    IFileStorageService fileStorage,
    IRequestValidator<RegisterOrganizationRequest> registerValidator,
    IRequestValidator<LoginRequest> loginValidator,
    OnboardingOptions options) : IAuthService
{
    public async Task<AuthenticationSession> RegisterOrganizationAsync(
        RegisterOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        registerValidator.ValidateAndThrow(request);
        string normalizedEmail = NormalizeEmail(request.ManagerEmail);
        if (await unitOfWork.Repository<User>().AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new ConflictException("A user with this email already exists.", "duplicate_email");
        }

        SubscriptionPlan? plan = await unitOfWork.Repository<SubscriptionPlan>()
            .FirstOrDefaultAsync(entity => entity.Code == options.DefaultPlanCode && entity.IsActive, cancellationToken);
        if (plan is null)
        {
            throw new ConflictException("The default onboarding plan is unavailable.", "default_plan_unavailable");
        }

        DateTimeOffset now = clock.UtcNow;
        Organization organization = new(request.OrganizationName, request.Timezone, request.DefaultLanguage)
        {
            LegalName = request.LegalName?.Trim(),
            Phone = request.Phone?.Trim(),
        };
        User manager = new(
            request.ManagerFullName,
            request.ManagerEmail,
            passwordService.HashPassword(request.Password),
            request.DefaultLanguage)
        {
            Phone = request.Phone?.Trim(),
        };
        organization.CreatedBy = manager.Id;
        Branch branch = new(organization.Id, options.PrimaryBranchName, request.Timezone)
        {
            IsPrimary = true,
        };
        OrganizationMember membership = new(organization.Id, manager.Id, OrganizationRole.Manager, now);
        OrganizationSubscription subscription = new()
        {
            OrganizationId = organization.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Trial,
            BillingMode = BillingMode.Trial,
            TrialStartedAt = now,
            TrialEndsAt = now.AddDays(options.TrialDays),
            GracePeriodEndsAt = now.AddDays(options.TrialDays + plan.GracePeriodDays),
        };

        //await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        //try
        //{
        await unitOfWork.Repository<Organization>().AddAsync(organization, cancellationToken);
        await unitOfWork.Repository<User>().AddAsync(manager, cancellationToken);
        await unitOfWork.Repository<Branch>().AddAsync(branch, cancellationToken);
        await unitOfWork.Repository<OrganizationMember>().AddAsync(membership, cancellationToken);
        await unitOfWork.Repository<OrganizationSubscription>().AddAsync(subscription, cancellationToken);
        await auditService.RecordTenantAsync(
            organization.Id,
            "organization.registered",
            nameof(Organization),
            organization.Id,
            newValues: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = organization.Name,
                ["managerUserId"] = manager.Id.ToString(),
            },
            cancellationToken: cancellationToken);

        AuthenticationSession session = await CreateOrganizationSessionAsync(
            manager,
            membership,
            organization,
            new SubscriptionAccess(SubscriptionAccessMode.Full, subscription.Status, subscription.TrialEndsAt, null),
            null,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        //await transaction.CommitAsync(cancellationToken);
        return session;
        //}
        //catch
        //{
        //    await transaction.RollbackAsync(cancellationToken);
        //    throw;
        //}
    }

    public async Task<AuthenticationSession> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        loginValidator.ValidateAndThrow(request);
        string normalizedEmail = NormalizeEmail(request.Email);
        User? user = await unitOfWork.Repository<User>()
            .FirstOrDefaultAsync(entity => entity.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null || !passwordService.VerifyPassword(user.PasswordHash, request.Password))
        {
            throw new AuthenticationFailedException();
        }

        if (user.AccountStatus != UserAccountStatus.Active)
        {
            throw new AuthenticationFailedException("The user account is not active.");
        }

        Guid targetOrgId;
        if (request.OrganizationId.HasValue && request.OrganizationId.Value != Guid.Empty)
        {
            targetOrgId = request.OrganizationId.Value;
        }
        else
        {
            OrganizationMember? activeMember;
            using (authenticationTenantScope.BeginBypass())
            {
                activeMember = await unitOfWork.Repository<OrganizationMember>()
                    .FirstOrDefaultAsync(m => m.UserId == user.Id && m.IsActive, cancellationToken);
            }

            if (activeMember is null)
            {
                throw new AuthenticationFailedException("User does not belong to any active organization.");
            }

            targetOrgId = activeMember.OrganizationId;
        }

        using IDisposable tenantScope = authenticationTenantScope.Begin(targetOrgId);
        (Organization organization, OrganizationMember membership, SubscriptionAccess access) =
            await LoadOrganizationAccessAsync(user.Id, targetOrgId, cancellationToken);
        if (access.Mode == SubscriptionAccessMode.Blocked)
        {
            throw new AuthenticationFailedException(access.Reason ?? "Organization access is blocked.");
        }

        user.LastLoginAt = clock.UtcNow;
        unitOfWork.Repository<User>().Update(user);
        AuthenticationSession session = await CreateOrganizationSessionAsync(
            user,
            membership,
            organization,
            access,
            null,
            cancellationToken);
        await auditService.RecordTenantAsync(
            organization.Id,
            "auth.login",
            nameof(User),
            user.Id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<AuthenticationSession> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        string tokenHash = tokenService.HashRefreshToken(refreshToken);
        RefreshToken? storedToken = await unitOfWork.Repository<RefreshToken>()
            .FirstOrDefaultAsync(entity => entity.TokenHash == tokenHash, cancellationToken);
        if (storedToken is null || storedToken.UserId is null || storedToken.OrganizationId is null)
        {
            throw new AuthenticationFailedException("The refresh token is invalid.");
        }

        DateTimeOffset now = clock.UtcNow;
        if (storedToken.RevokedAt is not null)
        {
            await RevokeTokenFamilyAsync(storedToken.FamilyId, "refresh-token reuse detected", cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException("Refresh-token reuse was detected.");
        }

        if (storedToken.ExpiresAt <= now)
        {
            throw new AuthenticationFailedException("The refresh token has expired.");
        }

        User? user = await unitOfWork.Repository<User>().GetByIdAsync(storedToken.UserId.Value, cancellationToken);
        if (user is null || user.AccountStatus != UserAccountStatus.Active)
        {
            throw new AuthenticationFailedException();
        }

        using IDisposable tenantScope = authenticationTenantScope.Begin(storedToken.OrganizationId.Value);
        (Organization organization, OrganizationMember membership, SubscriptionAccess access) =
            await LoadOrganizationAccessAsync(user.Id, storedToken.OrganizationId.Value, cancellationToken);
        if (access.Mode == SubscriptionAccessMode.Blocked)
        {
            throw new AuthenticationFailedException(access.Reason ?? "Organization access is blocked.");
        }

        return await RotateOrganizationSessionAsync(
            storedToken,
            user,
            membership,
            organization,
            access,
            cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        string tokenHash = tokenService.HashRefreshToken(refreshToken);
        RefreshToken? storedToken = await unitOfWork.Repository<RefreshToken>()
            .FirstOrDefaultAsync(entity => entity.TokenHash == tokenHash, cancellationToken);
        if (storedToken is null || storedToken.RevokedAt is not null)
        {
            return;
        }

        storedToken.Revoke(clock.UtcNow, currentUser.IpAddress, null, "logout");
        unitOfWork.Repository<RefreshToken>().Update(storedToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<MeResponse> GetMeAsync(CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        Guid userId = currentUser.UserId!.Value;
        User? user = await unitOfWork.Repository<User>().GetByIdAsync(userId, cancellationToken);
        (Organization organization, OrganizationMember membership, SubscriptionAccess access) =
            await LoadOrganizationAccessAsync(userId, organizationId, cancellationToken);
        if (user is null)
        {
            throw new EntityNotFoundException(nameof(User));
        }

        IReadOnlyList<Guid> departmentIds = await unitOfWork.Repository<UserDepartment>()
            .ProjectAsync(
                entity => entity.OrganizationId == organizationId && entity.UserId == userId && entity.LeftAt == null,
                entity => entity.DepartmentId,
                cancellationToken);
        return new MeResponse(
            MapUser(user),
            MapOrganization(organization),
            MapMembership(membership),
            departmentIds,
            access);
    }

    private async Task<(Organization Organization, OrganizationMember Membership, SubscriptionAccess Access)> LoadOrganizationAccessAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        OrganizationMember? membership = await unitOfWork.Repository<OrganizationMember>()
            .FirstOrDefaultAsync(
                entity => entity.OrganizationId == organizationId && entity.UserId == userId && entity.IsActive,
                cancellationToken);
        Organization? organization = await unitOfWork.Repository<Organization>().GetByIdAsync(organizationId, cancellationToken);
        if (membership is null || organization is null || organization.Status != OrganizationStatus.Active)
        {
            throw new AuthenticationFailedException("The organization membership is not active.");
        }

        SubscriptionAccess access = await subscriptionAccessService.GetAccessAsync(organizationId, cancellationToken);
        return (organization, membership, access);
    }

    private async Task<AuthenticationSession> RotateOrganizationSessionAsync(
        RefreshToken storedToken,
        User user,
        OrganizationMember membership,
        Organization organization,
        SubscriptionAccess access,
        CancellationToken cancellationToken)
    {
        Guid replacementId = Guid.NewGuid();
        AuthenticationSession session = await CreateOrganizationSessionAsync(
            user,
            membership,
            organization,
            access,
            storedToken.FamilyId,
            cancellationToken,
            replacementId);
        storedToken.Revoke(clock.UtcNow, currentUser.IpAddress, replacementId, "rotated");
        unitOfWork.Repository<RefreshToken>().Update(storedToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session;
    }

    private async Task<AuthenticationSession> CreateOrganizationSessionAsync(
        User user,
        OrganizationMember membership,
        Organization organization,
        SubscriptionAccess access,
        Guid? familyId,
        CancellationToken cancellationToken,
        Guid? refreshTokenId = null)
    {
        DateTimeOffset now = clock.UtcNow;
        IssuedAccessToken accessToken = tokenService.CreateAccessToken(new AccessTokenDescriptor(
            user.Id,
            user.Email ?? string.Empty,
            now,
            organization.Id,
            membership.Role));
        string rawRefreshToken = tokenService.GenerateRefreshToken();
        DateTimeOffset refreshExpiresAt = now.Add(tokenService.RefreshTokenLifetime);
        RefreshToken refreshToken = new(
            user.Id,
            null,
            organization.Id,
            familyId ?? Guid.NewGuid(),
            tokenService.HashRefreshToken(rawRefreshToken),
            refreshExpiresAt)
        {
            Id = refreshTokenId ?? Guid.NewGuid(),
            CreatedByIp = currentUser.IpAddress,
        };
        await unitOfWork.Repository<RefreshToken>().AddAsync(refreshToken, cancellationToken);
        return new AuthenticationSession(
            new AuthenticationResponse(
                accessToken.Token,
                accessToken.ExpiresAt,
                MapUser(user),
                MapOrganization(organization),
                MapMembership(membership),
                access),
            rawRefreshToken,
            refreshExpiresAt);
    }

    private async Task RevokeTokenFamilyAsync(Guid familyId, string reason, CancellationToken cancellationToken)
    {
        PagedResult<RefreshToken> family = await unitOfWork.Repository<RefreshToken>()
            .ListAsync(entity => entity.FamilyId == familyId, new PageRequest(1, PageRequest.MaximumPageSize), cancellationToken);
        foreach (RefreshToken token in family.Items.Where(token => token.RevokedAt is null))
        {
            token.Revoke(clock.UtcNow, currentUser.IpAddress, null, reason);
            unitOfWork.Repository<RefreshToken>().Update(token);
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private CurrentUserDto MapUser(User user)
    {
        string? avatarUrl = string.IsNullOrWhiteSpace(user.ProfileImageUrl)
            ? null
            : fileStorage.ResolveUrl(user.ProfileImageUrl);
        return new(user.Id, user.FullName, user.Email, user.Phone, user.Address, avatarUrl, user.PreferredLanguage, user.AccountStatus);
    }

    private static OrganizationSummaryDto MapOrganization(Organization organization) =>
        new(organization.Id, organization.Name, organization.LegalName, organization.Timezone, organization.DefaultLanguage, organization.Status);

    private static MembershipDto MapMembership(OrganizationMember membership) =>
        new(membership.Id, membership.Role, membership.IsActive, membership.JoinedAt);

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        Guid userId = AuthorizationGuard.RequireUser(currentUser);
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            throw Validation(nameof(request.CurrentPassword), "Current password is required.");
        }
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            throw Validation(nameof(request.NewPassword), "New password must be at least 8 characters.");
        }

        User user = await unitOfWork.Repository<User>().GetByIdAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(User));

        if (!passwordService.VerifyPassword(user.PasswordHash, request.CurrentPassword))
        {
            throw Validation(nameof(request.CurrentPassword), "Current password is incorrect.");
        }

        user.PasswordHash = passwordService.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        unitOfWork.Repository<User>().Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<CurrentUserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        Guid userId = AuthorizationGuard.RequireUser(currentUser);
        User user = await unitOfWork.Repository<User>().GetByIdAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(User));
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw Validation(nameof(request.FullName), "Full name is required.");
        }

        user.FullName = request.FullName.Trim();
        user.Phone = request.Phone?.Trim();
        user.Address = request.Address?.Trim();
        if (request.ProfileImageUrl is not null)
        {
            user.ProfileImageUrl = request.ProfileImageUrl.Trim();
        }
        if (!string.IsNullOrWhiteSpace(request.PreferredLanguage))
        {
            user.PreferredLanguage = request.PreferredLanguage;
        }

        unitOfWork.Repository<User>().Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MapUser(user);
    }

    public async Task<StoredFile> UploadMyAvatarAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        Guid userId = AuthorizationGuard.RequireUser(currentUser);
        User user = await unitOfWork.Repository<User>().GetByIdAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(User));
        StoredFile file = await fileStorage.SaveAsync(content, fileName, contentType, cancellationToken);
        user.ProfileImageUrl = file.Url;
        unitOfWork.Repository<User>().Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return file;
    }

    private static RequestValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
