using OpsManager.Domain.Entities;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Organizations;
using OpsManager.Service.Organizations.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Platform;

public interface IPlatformBranchService
{
    Task<PagedResponse<BranchDto>> ListAsync(
        Guid organizationId,
        PageQuery page,
        CancellationToken cancellationToken = default);

    Task<BranchDto> AddAsync(
        Guid organizationId,
        SaveBranchRequest request,
        CancellationToken cancellationToken = default);

    Task<BranchDto> UpdateAsync(
        Guid organizationId,
        Guid branchId,
        SaveBranchRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken = default);
}

public sealed class PlatformBranchService(
    IUnitOfWork unitOfWork,
    ICurrentUserContext currentUser,
    IAuditService auditService,
    IRequestValidator<SaveBranchRequest> branchValidator) : IPlatformBranchService
{
    public async Task<PagedResponse<BranchDto>> ListAsync(
        Guid organizationId,
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        _ = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        await EnsureOrganizationExistsAsync(organizationId, cancellationToken);
        PagedResult<Branch> result = await unitOfWork.Repository<Branch>()
            .ListAsync(
                branch => branch.OrganizationId == organizationId,
                page.ToDomain(),
                cancellationToken);
        return PagedResponse.Map(result, Map);
    }

    public async Task<BranchDto> AddAsync(
        Guid organizationId,
        SaveBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        branchValidator.ValidateAndThrow(request);
        await EnsureOrganizationExistsAsync(organizationId, cancellationToken);
        if (request.IsActive)
        {
            await EnsureBranchLimitAllowsActivationAsync(organizationId, cancellationToken);
        }

        await EnsureUniqueNameAsync(organizationId, request.Name, null, cancellationToken);
        Branch branch = new(organizationId, request.Name, request.Timezone)
        {
            Address = request.Address?.Trim(),
            Phone = request.Phone?.Trim(),
            IsPrimary = request.IsPrimary,
            IsActive = request.IsActive,
        };
        if (branch.IsPrimary)
        {
            await ClearExistingPrimaryAsync(organizationId, cancellationToken);
        }

        await unitOfWork.Repository<Branch>().AddAsync(branch, cancellationToken);
        await auditService.RecordPlatformAsync(
            "organization-branch.created",
            nameof(Branch),
            branch.Id,
            organizationId,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(branch);
    }

    public async Task<BranchDto> UpdateAsync(
        Guid organizationId,
        Guid branchId,
        SaveBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        branchValidator.ValidateAndThrow(request);
        await EnsureOrganizationExistsAsync(organizationId, cancellationToken);
        Branch branch = await GetBranchAsync(organizationId, branchId, cancellationToken);
        if (!branch.IsActive && request.IsActive)
        {
            await EnsureBranchLimitAllowsActivationAsync(organizationId, cancellationToken);
        }

        await EnsureUniqueNameAsync(organizationId, request.Name, branchId, cancellationToken);
        branch.Name = request.Name.Trim();
        branch.Address = request.Address?.Trim();
        branch.Phone = request.Phone?.Trim();
        branch.Timezone = request.Timezone;
        branch.IsActive = request.IsActive;
        if (request.IsPrimary && !branch.IsPrimary)
        {
            await ClearExistingPrimaryAsync(organizationId, cancellationToken);
        }

        branch.IsPrimary = request.IsPrimary;
        unitOfWork.Repository<Branch>().Update(branch);
        await auditService.RecordPlatformAsync(
            "organization-branch.updated",
            nameof(Branch),
            branch.Id,
            organizationId,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(branch);
    }

    public async Task DeleteAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        _ = AuthorizationGuard.RequirePlatformAdministrator(currentUser);
        await EnsureOrganizationExistsAsync(organizationId, cancellationToken);
        Branch branch = await GetBranchAsync(organizationId, branchId, cancellationToken);
        int activeBranches = await unitOfWork.Repository<Branch>()
            .CountAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.IsActive,
                cancellationToken);
        if (branch.IsActive && activeBranches <= 1)
        {
            throw new ConflictException(
                "The only active branch cannot be deleted.",
                "last_active_branch");
        }

        if (await unitOfWork.Repository<Department>()
            .AnyAsync(department => department.BranchId == branchId, cancellationToken))
        {
            throw new ConflictException(
                "A branch with departments cannot be deleted.",
                "branch_has_dependencies");
        }

        unitOfWork.Repository<Branch>().Remove(branch);
        await auditService.RecordPlatformAsync(
            "organization-branch.deleted",
            nameof(Branch),
            branch.Id,
            organizationId,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureBranchLimitAllowsActivationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        OrganizationSubscription? subscription =
            await unitOfWork.Repository<OrganizationSubscription>()
                .FirstOrDefaultAsync(
                    candidate => candidate.OrganizationId == organizationId,
                    cancellationToken);
        SubscriptionPlan? plan = subscription is null
            ? null
            : await unitOfWork.Repository<SubscriptionPlan>()
                .GetByIdAsync(subscription.PlanId, cancellationToken);
        if (subscription is null || plan is null)
        {
            throw new SubscriptionRestrictionException(
                "The organization has no valid plan.");
        }

        int activeBranches = await unitOfWork.Repository<Branch>()
            .CountAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.IsActive,
                cancellationToken);
        if (activeBranches >= plan.MaxBranches)
        {
            throw new SubscriptionRestrictionException(
                "The plan branch limit has been reached.",
                "branch_limit_reached");
        }
    }

    private async Task EnsureOrganizationExistsAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (await unitOfWork.Repository<Organization>()
                .GetByIdAsync(organizationId, cancellationToken) is null)
        {
            throw new EntityNotFoundException(nameof(Organization));
        }
    }

    private async Task<Branch> GetBranchAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken) =>
        await unitOfWork.Repository<Branch>().FirstOrDefaultAsync(
            branch => branch.Id == branchId && branch.OrganizationId == organizationId,
            cancellationToken)
        ?? throw new EntityNotFoundException(nameof(Branch));

    private async Task EnsureUniqueNameAsync(
        Guid organizationId,
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        string normalizedName = name.Trim().ToUpperInvariant();
#pragma warning disable CA1862 // Keep query provider translation server-side.
        bool exists = await unitOfWork.Repository<Branch>().AnyAsync(
            branch =>
                branch.OrganizationId == organizationId &&
                branch.Id != excludedId &&
                branch.Name.ToUpperInvariant() == normalizedName,
            cancellationToken);
#pragma warning restore CA1862
        if (exists)
        {
            throw new ConflictException(
                "A branch with this name already exists.",
                "duplicate_branch_name");
        }
    }

    private async Task ClearExistingPrimaryAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        Branch? existing = await unitOfWork.Repository<Branch>().FirstOrDefaultAsync(
            branch => branch.OrganizationId == organizationId && branch.IsPrimary,
            cancellationToken);
        if (existing is not null)
        {
            existing.IsPrimary = false;
            unitOfWork.Repository<Branch>().Update(existing);
        }
    }

    private static BranchDto Map(Branch branch) =>
        new(
            branch.Id,
            branch.Name,
            branch.Address,
            branch.Phone,
            branch.Timezone,
            branch.IsPrimary,
            branch.IsActive);
}
