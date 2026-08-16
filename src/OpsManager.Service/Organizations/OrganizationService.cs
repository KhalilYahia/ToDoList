using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Organizations.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Organizations;

public interface IOrganizationService
{
    Task<OrganizationDto> GetOrganizationAsync(CancellationToken cancellationToken = default);
    Task<OrganizationDto> UpdateOrganizationAsync(UpdateOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<PagedResponse<BranchDto>> ListBranchesAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task<BranchDto> GetBranchAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResponse<DepartmentDto>> ListDepartmentsAsync(PageQuery page, Guid? branchId, CancellationToken cancellationToken = default);
    Task<DepartmentDto> CreateDepartmentAsync(SaveDepartmentRequest request, CancellationToken cancellationToken = default);
    Task<DepartmentDto> GetDepartmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DepartmentDto> UpdateDepartmentAsync(Guid id, SaveDepartmentRequest request, CancellationToken cancellationToken = default);
    Task DeleteDepartmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResponse<MemberDto>> ListMembersAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task<MemberDto> CreateMemberAsync(CreateMemberRequest request, CancellationToken cancellationToken = default);
    Task<MemberDto> GetMemberAsync(Guid membershipId, CancellationToken cancellationToken = default);
    Task<MemberDto> UpdateMemberAsync(Guid membershipId, UpdateMemberRequest request, CancellationToken cancellationToken = default);
    Task ActivateMemberAsync(Guid membershipId, CancellationToken cancellationToken = default);
    Task SuspendMemberAsync(Guid membershipId, CancellationToken cancellationToken = default);
    Task SetMemberDepartmentsAsync(Guid membershipId, SetMemberDepartmentsRequest request, CancellationToken cancellationToken = default);
    Task ResetMemberPasswordAsync(Guid membershipId, ResetMemberPasswordRequest request, CancellationToken cancellationToken = default);
    Task<StoredFile> UploadMemberAvatarAsync(Guid membershipId, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
}

public sealed class OrganizationService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    ISubscriptionAccessService subscriptionAccess,
    IPasswordService passwordService,
    IClock clock,
    IAuditService auditService,
    IFileStorageService fileStorage,
    IRequestValidator<UpdateOrganizationRequest> organizationValidator,
    IRequestValidator<SaveDepartmentRequest> departmentValidator,
    IRequestValidator<CreateMemberRequest> createMemberValidator,
    IRequestValidator<UpdateMemberRequest> updateMemberValidator) : IOrganizationService
{
    public async Task<OrganizationDto> GetOrganizationAsync(CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        return Map(await GetOrganizationEntityAsync(organizationId, cancellationToken));
    }

    public async Task<OrganizationDto> UpdateOrganizationAsync(
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        organizationValidator.ValidateAndThrow(request);
        Organization organization = await GetOrganizationEntityAsync(organizationId, cancellationToken);
        Dictionary<string, string> oldValues = new(StringComparer.Ordinal)
        {
            ["name"] = organization.Name,
            ["timezone"] = organization.Timezone,
            ["defaultLanguage"] = organization.DefaultLanguage,
        };
        organization.Name = request.Name.Trim();
        organization.LegalName = request.LegalName?.Trim();
        organization.LogoUrl = request.LogoUrl?.Trim();
        organization.Phone = request.Phone?.Trim();
        organization.Email = request.Email?.Trim();
        organization.Timezone = request.Timezone;
        organization.ChangeDefaultLanguage(request.DefaultLanguage);
        unitOfWork.Repository<Organization>().Update(organization);
        await auditService.RecordTenantAsync(organizationId, "organization.updated", nameof(Organization), organization.Id, oldValues,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = organization.Name,
                ["timezone"] = organization.Timezone,
                ["defaultLanguage"] = organization.DefaultLanguage,
            }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(organization);
    }

    public async Task<PagedResponse<BranchDto>> ListBranchesAsync(PageQuery page, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        PagedResult<Branch> result = await unitOfWork.Repository<Branch>()
            .ListAsync(entity => entity.OrganizationId == organizationId, page.ToDomain(), cancellationToken);
        return PagedResponse.Map(result, Map);
    }

    public async Task<BranchDto> GetBranchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        return Map(await GetBranchEntityAsync(id, organizationId, cancellationToken));
    }

    public async Task<PagedResponse<DepartmentDto>> ListDepartmentsAsync(
        PageQuery page,
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        PagedResult<Department> result = await unitOfWork.Repository<Department>()
            .ListAsync(
                entity => entity.OrganizationId == organizationId && (branchId == null || entity.BranchId == branchId),
                page.ToDomain(),
                cancellationToken);
        return PagedResponse.Map(result, Map);
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(
        SaveDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        departmentValidator.ValidateAndThrow(request);
        _ = await GetBranchEntityAsync(request.BranchId, organizationId, cancellationToken);
        await ValidateSupervisorAsync(request.SupervisorUserId, organizationId, cancellationToken);
        await EnsureUniqueDepartmentNameAsync(request.BranchId, request.Name, null, cancellationToken);
        Department department = new(organizationId, request.BranchId, request.Name)
        {
            Description = request.Description?.Trim(),
            SupervisorUserId = request.SupervisorUserId,
            IsActive = request.IsActive,
        };
        await unitOfWork.Repository<Department>().AddAsync(department, cancellationToken);
        await auditService.RecordTenantAsync(organizationId, "department.created", nameof(Department), department.Id, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(department);
    }

    public async Task<DepartmentDto> GetDepartmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireOrganization(currentUser);
        return Map(await GetDepartmentEntityAsync(id, organizationId, cancellationToken));
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(
        Guid id,
        SaveDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        departmentValidator.ValidateAndThrow(request);
        Department department = await GetDepartmentEntityAsync(id, organizationId, cancellationToken);
        _ = await GetBranchEntityAsync(request.BranchId, organizationId, cancellationToken);
        await ValidateSupervisorAsync(request.SupervisorUserId, organizationId, cancellationToken);
        await EnsureUniqueDepartmentNameAsync(request.BranchId, request.Name, id, cancellationToken);
        department.BranchId = request.BranchId;
        department.Name = request.Name.Trim();
        department.Description = request.Description?.Trim();
        department.SupervisorUserId = request.SupervisorUserId;
        department.IsActive = request.IsActive;
        unitOfWork.Repository<Department>().Update(department);
        await auditService.RecordTenantAsync(organizationId, "department.updated", nameof(Department), department.Id, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(department);
    }

    public async Task DeleteDepartmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        Department department = await GetDepartmentEntityAsync(id, organizationId, cancellationToken);
        bool hasDependencies = await unitOfWork.Repository<TaskTemplate>().AnyAsync(entity => entity.DefaultDepartmentId == id, cancellationToken)
            || await unitOfWork.Repository<OperationalTask>().AnyAsync(entity => entity.DepartmentId == id, cancellationToken)
            || await unitOfWork.Repository<OrderTemplate>().AnyAsync(entity => entity.SourceDepartmentId == id || entity.TargetDepartmentId == id, cancellationToken)
            || await unitOfWork.Repository<DepartmentOrder>().AnyAsync(entity => entity.SourceDepartmentId == id || entity.TargetDepartmentId == id, cancellationToken);
        if (hasDependencies)
        {
            throw new ConflictException("A department with operational history cannot be deleted.", "department_has_dependencies");
        }

        unitOfWork.Repository<Department>().Remove(department);
        await auditService.RecordTenantAsync(organizationId, "department.deleted", nameof(Department), department.Id, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResponse<MemberDto>> ListMembersAsync(PageQuery page, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireSupervisorOrManager(currentUser);
        PagedResult<OrganizationMember> memberships = await unitOfWork.Repository<OrganizationMember>()
            .ListAsync(entity => entity.OrganizationId == organizationId, page.ToDomain(), cancellationToken);
        IReadOnlyList<MemberDto> items = new List<MemberDto>();
        List<MemberDto> mapped = [];
        foreach (OrganizationMember membership in memberships.Items)
        {
            mapped.Add(await MapMemberAsync(membership, cancellationToken));
        }

        items = mapped;
        return new PagedResponse<MemberDto>(items, memberships.Page, memberships.PageSize, memberships.TotalCount);
    }

    public async Task<MemberDto> CreateMemberAsync(CreateMemberRequest request, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        createMemberValidator.ValidateAndThrow(request);
        (SubscriptionPlan plan, _) = await GetPlanAndSubscriptionAsync(organizationId, cancellationToken);
        int activeMembers = await unitOfWork.Repository<OrganizationMember>()
            .CountAsync(entity => entity.OrganizationId == organizationId && entity.IsActive, cancellationToken);
        if (activeMembers >= plan.MaxUsers)
        {
            throw new SubscriptionRestrictionException("The plan user limit has been reached.", "user_limit_reached");
        }

        string normalizedEmail = request.Email.Trim().ToUpperInvariant();
        if (await unitOfWork.Repository<User>().AnyAsync(entity => entity.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new ConflictException("A user with this email already exists.", "duplicate_email");
        }

        await ValidateDepartmentIdsAsync(request.DepartmentIds, organizationId, cancellationToken);
        User user = new(request.FullName, request.Email, passwordService.HashPassword(request.TemporaryPassword), request.PreferredLanguage)
        {
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            ProfileImageUrl = request.ProfileImageUrl?.Trim(),
            MustChangePassword = true,
        };
        OrganizationMember membership = new(organizationId, user.Id, request.Role, clock.UtcNow);
        await unitOfWork.ExecuteWithStrategyAsync(async () =>
        {
            await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await unitOfWork.Repository<User>().AddAsync(user, cancellationToken);
                await unitOfWork.Repository<OrganizationMember>().AddAsync(membership, cancellationToken);
                await AddDepartmentAssignmentsAsync(user.Id, organizationId, request.DepartmentIds, cancellationToken);
                await auditService.RecordTenantAsync(organizationId, "member.created", nameof(OrganizationMember), membership.Id, cancellationToken: cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });


        return await MapMemberAsync(membership, cancellationToken);
    }

    public async Task<MemberDto> GetMemberAsync(Guid membershipId, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireSupervisorOrManager(currentUser);
        OrganizationMember membership = await GetMembershipAsync(membershipId, organizationId, cancellationToken);
        return await MapMemberAsync(membership, cancellationToken);
    }

    public async Task<MemberDto> UpdateMemberAsync(
        Guid membershipId,
        UpdateMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        updateMemberValidator.ValidateAndThrow(request);
        OrganizationMember membership = await GetMembershipAsync(membershipId, organizationId, cancellationToken);
        if (membership.Role == OrganizationRole.Manager && request.Role != OrganizationRole.Manager)
        {
            await EnsureAnotherActiveManagerAsync(organizationId, membership.Id, cancellationToken);
        }

        User? user = await unitOfWork.Repository<User>().GetByIdAsync(membership.UserId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(User));
        user.FullName = request.FullName.Trim();
        user.Phone = request.Phone?.Trim();
        user.Address = request.Address?.Trim();
        if (request.ProfileImageUrl is not null)
        {
            user.ProfileImageUrl = request.ProfileImageUrl.Trim();
        }
        user.PreferredLanguage = request.PreferredLanguage;
        membership.Role = request.Role;
        unitOfWork.Repository<User>().Update(user);
        unitOfWork.Repository<OrganizationMember>().Update(membership);
        await auditService.RecordTenantAsync(organizationId, "member.updated", nameof(OrganizationMember), membership.Id, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapMemberAsync(membership, cancellationToken);
    }

    public Task ActivateMemberAsync(Guid membershipId, CancellationToken cancellationToken = default) =>
        SetMemberActiveAsync(membershipId, true, cancellationToken);

    public Task SuspendMemberAsync(Guid membershipId, CancellationToken cancellationToken = default) =>
        SetMemberActiveAsync(membershipId, false, cancellationToken);

    public async Task SetMemberDepartmentsAsync(
        Guid membershipId,
        SetMemberDepartmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        OrganizationMember membership = await GetMembershipAsync(membershipId, organizationId, cancellationToken);
        Guid[] departmentIds = request.DepartmentIds.Distinct().ToArray();
        await ValidateDepartmentIdsAsync(departmentIds, organizationId, cancellationToken);
        PagedResult<UserDepartment> existing = await unitOfWork.Repository<UserDepartment>()
            .ListAsync(entity => entity.OrganizationId == organizationId && entity.UserId == membership.UserId && entity.LeftAt == null,
                new PageRequest(1, PageRequest.MaximumPageSize), cancellationToken);
        DateTimeOffset now = clock.UtcNow;
        foreach (UserDepartment assignment in existing.Items)
        {
            assignment.LeftAt = now;
            assignment.IsPrimary = false;
            unitOfWork.Repository<UserDepartment>().Update(assignment);
        }

        await AddDepartmentAssignmentsAsync(membership.UserId, organizationId, departmentIds, cancellationToken);
        await auditService.RecordTenantAsync(organizationId, "member.departments-updated", nameof(OrganizationMember), membership.Id, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetMemberPasswordAsync(Guid membershipId, ResetMemberPasswordRequest request, CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            throw Validation(nameof(request.NewPassword), "New password must be at least 8 characters.");
        }

        OrganizationMember membership = await GetMembershipAsync(membershipId, organizationId, cancellationToken);
        User user = await unitOfWork.Repository<User>().GetByIdAsync(membership.UserId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(User));

        user.PasswordHash = passwordService.HashPassword(request.NewPassword);
        user.MustChangePassword = true;
        unitOfWork.Repository<User>().Update(user);
        await auditService.RecordTenantAsync(organizationId, "member.password-reset", nameof(OrganizationMember), membershipId, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoredFile> UploadMemberAvatarAsync(
        Guid membershipId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        OrganizationMember membership = await GetMembershipAsync(membershipId, organizationId, cancellationToken);
        User user = await unitOfWork.Repository<User>().GetByIdAsync(membership.UserId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(User));
        StoredFile file = await fileStorage.SaveAsync(content, fileName, contentType, cancellationToken);
        user.ProfileImageUrl = file.Url;
        unitOfWork.Repository<User>().Update(user);
        await auditService.RecordTenantAsync(organizationId, "member.avatar-updated", nameof(OrganizationMember), membershipId, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return file;
    }

    private static RequestValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private async Task SetMemberActiveAsync(Guid membershipId, bool isActive, CancellationToken cancellationToken)
    {
        Guid organizationId = AuthorizationGuard.RequireManager(currentUser);
        await subscriptionAccess.EnsureWriteAllowedAsync(organizationId, cancellationToken: cancellationToken);
        OrganizationMember membership = await GetMembershipAsync(membershipId, organizationId, cancellationToken);
        if (!isActive && membership.Role == OrganizationRole.Manager)
        {
            await EnsureAnotherActiveManagerAsync(organizationId, membership.Id, cancellationToken);
        }

        membership.IsActive = isActive;
        membership.LeftAt = isActive ? null : clock.UtcNow;
        unitOfWork.Repository<OrganizationMember>().Update(membership);
        await auditService.RecordTenantAsync(organizationId, isActive ? "member.activated" : "member.suspended", nameof(OrganizationMember), membership.Id, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task AddDepartmentAssignmentsAsync(
        Guid userId,
        Guid organizationId,
        IReadOnlyCollection<Guid> departmentIds,
        CancellationToken cancellationToken)
    {
        int index = 0;
        foreach (Guid departmentId in departmentIds.Distinct())
        {
            await unitOfWork.Repository<UserDepartment>().AddAsync(new UserDepartment(organizationId, userId, departmentId, clock.UtcNow)
            {
                IsPrimary = index++ == 0,
            }, cancellationToken);
        }
    }

    private async Task ValidateDepartmentIdsAsync(
        IReadOnlyCollection<Guid> departmentIds,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        foreach (Guid departmentId in departmentIds.Distinct())
        {
            _ = await GetDepartmentEntityAsync(departmentId, organizationId, cancellationToken);
        }
    }

    private async Task ValidateSupervisorAsync(Guid? supervisorUserId, Guid organizationId, CancellationToken cancellationToken)
    {
        if (supervisorUserId is null)
        {
            return;
        }

        bool valid = await unitOfWork.Repository<OrganizationMember>().AnyAsync(
            entity => entity.OrganizationId == organizationId && entity.UserId == supervisorUserId && entity.IsActive &&
                      (entity.Role == OrganizationRole.Manager || entity.Role == OrganizationRole.Supervisor),
            cancellationToken);
        if (!valid)
        {
            throw new ConflictException("The supervisor must be an active Manager or Supervisor in this organization.", "invalid_supervisor");
        }
    }

    private async Task EnsureAnotherActiveManagerAsync(Guid organizationId, Guid excludedMembershipId, CancellationToken cancellationToken)
    {
        int otherManagers = await unitOfWork.Repository<OrganizationMember>().CountAsync(
            entity => entity.OrganizationId == organizationId && entity.Id != excludedMembershipId && entity.IsActive && entity.Role == OrganizationRole.Manager,
            cancellationToken);
        if (otherManagers == 0)
        {
            throw new ConflictException("The last active manager cannot be removed, suspended, or demoted.", "last_active_manager");
        }
    }

    private async Task EnsureUniqueDepartmentNameAsync(Guid branchId, string name, Guid? excludedId, CancellationToken cancellationToken)
    {
        string normalized = name.Trim().ToUpperInvariant();
#pragma warning disable CA1304 // EF Core translates parameterless ToUpper() to SQL UPPER().
#pragma warning disable CA1311 // Culture overload cannot be translated by EF Core.
#pragma warning disable CA1862 // Keep query provider translation server-side.
        bool exists = await unitOfWork.Repository<Department>().AnyAsync(
            entity => entity.BranchId == branchId && entity.Id != excludedId && entity.Name.ToUpper() == normalized,
            cancellationToken);
#pragma warning restore CA1862
#pragma warning restore CA1311
#pragma warning restore CA1304
        if (exists)
        {
            throw new ConflictException("A department with this name already exists in the branch.", "duplicate_department_name");
        }
    }

    private async Task<(SubscriptionPlan Plan, OrganizationSubscription Subscription)> GetPlanAndSubscriptionAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        OrganizationSubscription? subscription = await unitOfWork.Repository<OrganizationSubscription>()
            .FirstOrDefaultAsync(entity => entity.OrganizationId == organizationId, cancellationToken);
        SubscriptionPlan? plan = subscription is null
            ? null
            : await unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(subscription.PlanId, cancellationToken);
        if (subscription is null || plan is null)
        {
            throw new SubscriptionRestrictionException("The organization has no valid plan.");
        }

        return (plan, subscription);
    }

    private async Task<Organization> GetOrganizationEntityAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<Organization>().GetByIdAsync(organizationId, cancellationToken)
        ?? throw new EntityNotFoundException(nameof(Organization));

    private async Task<Branch> GetBranchEntityAsync(Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<Branch>().FirstOrDefaultAsync(entity => entity.Id == id && entity.OrganizationId == organizationId, cancellationToken)
        ?? throw new EntityNotFoundException(nameof(Branch));

    private async Task<Department> GetDepartmentEntityAsync(Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<Department>().FirstOrDefaultAsync(entity => entity.Id == id && entity.OrganizationId == organizationId, cancellationToken)
        ?? throw new EntityNotFoundException(nameof(Department));

    private async Task<OrganizationMember> GetMembershipAsync(Guid id, Guid organizationId, CancellationToken cancellationToken) =>
        await unitOfWork.Repository<OrganizationMember>().FirstOrDefaultAsync(entity => entity.Id == id && entity.OrganizationId == organizationId, cancellationToken)
        ?? throw new EntityNotFoundException(nameof(OrganizationMember));

    private async Task<MemberDto> MapMemberAsync(OrganizationMember membership, CancellationToken cancellationToken)
    {
        User? user = await unitOfWork.Repository<User>().GetByIdAsync(membership.UserId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(User));
        IReadOnlyList<Guid> departments = await unitOfWork.Repository<UserDepartment>().ProjectAsync(
            entity => entity.OrganizationId == membership.OrganizationId && entity.UserId == membership.UserId && entity.LeftAt == null,
            entity => entity.DepartmentId,
            cancellationToken);
        string? avatarUrl = string.IsNullOrWhiteSpace(user.ProfileImageUrl)
            ? null
            : fileStorage.ResolveUrl(user.ProfileImageUrl);
        return new MemberDto(
            membership.Id,
            user.Id,
            user.FullName,
            user.Email,
            user.Phone,
            user.Address,
            avatarUrl,
            membership.Role,
            membership.IsActive,
            user.AccountStatus,
            user.MustChangePassword,
            departments);
    }

    private static OrganizationDto Map(Organization entity) => new(
        entity.Id, entity.Name, entity.LegalName, entity.LogoUrl, entity.Phone, entity.Email,
        entity.Timezone, entity.DefaultLanguage, entity.Status);

    private static BranchDto Map(Branch entity) => new(
        entity.Id, entity.Name, entity.Address, entity.Phone, entity.Timezone, entity.IsPrimary, entity.IsActive);

    private static DepartmentDto Map(Department entity) => new(
        entity.Id, entity.BranchId, entity.Name, entity.Description, entity.SupervisorUserId, entity.IsActive);
}
