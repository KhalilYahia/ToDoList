using System.Linq.Expressions;
using OpsManager.Domain.Common;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Organizations;
using OpsManager.Service.Organizations.DTOs;
using OpsManager.Service.Platform;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Tests;

public sealed class PlatformBranchServiceTests
{
    [Fact]
    public async Task Administrator_can_add_update_list_and_delete_for_one_organization()
    {
        InMemoryUnitOfWork unitOfWork = new();
        Organization organization = new("Organization", "UTC", "en");
        Organization otherOrganization = new("Other", "UTC", "en");
        SubscriptionPlan plan = new() { Name = "Plan", Code = "plan", MaxBranches = 3 };
        OrganizationSubscription subscription = new()
        {
            OrganizationId = organization.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
        };
        Branch primary = new(organization.Id, "Primary", "UTC")
        {
            IsPrimary = true,
            IsActive = true,
        };
        Branch otherBranch = new(otherOrganization.Id, "Other branch", "UTC");
        unitOfWork.Seed(organization, otherOrganization, plan, subscription, primary, otherBranch);
        RecordingAuditService audit = new();
        PlatformBranchService service = CreateService(
            unitOfWork,
            TestCurrentUser.PlatformAdministrator(),
            audit);

        BranchDto created = await service.AddAsync(
            organization.Id,
            new SaveBranchRequest("Second", "Address", "123", "UTC", false));
        BranchDto updated = await service.UpdateAsync(
            organization.Id,
            created.Id,
            new SaveBranchRequest("Renamed", "New address", "456", "Europe/Moscow", false));
        PagedResponse<BranchDto> listed =
            await service.ListAsync(organization.Id, new PageQuery(1, 20));
        await service.DeleteAsync(organization.Id, created.Id);

        Assert.Equal("Renamed", updated.Name);
        Assert.Equal("Europe/Moscow", updated.Timezone);
        Assert.Equal(2, listed.TotalCount);
        Assert.DoesNotContain(listed.Items, branch => branch.Id == otherBranch.Id);
        Assert.DoesNotContain(
            unitOfWork.Items<Branch>(),
            branch => branch.Id == created.Id);
        Assert.Equal(
            [
                "organization-branch.created",
                "organization-branch.updated",
                "organization-branch.deleted",
            ],
            audit.PlatformActions);
    }

    [Fact]
    public async Task Organization_manager_is_rejected_by_every_platform_branch_operation()
    {
        InMemoryUnitOfWork unitOfWork = new();
        Guid organizationId = Guid.NewGuid();
        PlatformBranchService service = CreateService(
            unitOfWork,
            TestCurrentUser.OrganizationManager(organizationId),
            new RecordingAuditService());
        SaveBranchRequest request = new("Branch", null, null, "UTC", false);
        Guid branchId = Guid.NewGuid();

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.ListAsync(organizationId, new PageQuery()));
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.AddAsync(organizationId, request));
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.UpdateAsync(organizationId, branchId, request));
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.DeleteAsync(organizationId, branchId));
    }

    [Fact]
    public async Task Active_branch_creation_respects_the_organization_plan_limit()
    {
        InMemoryUnitOfWork unitOfWork = new();
        Organization organization = new("Organization", "UTC", "en");
        SubscriptionPlan plan = new() { Name = "Plan", Code = "plan", MaxBranches = 1 };
        OrganizationSubscription subscription = new()
        {
            OrganizationId = organization.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
        };
        Branch existing = new(organization.Id, "Existing", "UTC");
        unitOfWork.Seed(organization, plan, subscription, existing);
        PlatformBranchService service = CreateService(
            unitOfWork,
            TestCurrentUser.PlatformAdministrator(),
            new RecordingAuditService());

        SubscriptionRestrictionException exception =
            await Assert.ThrowsAsync<SubscriptionRestrictionException>(
                () => service.AddAsync(
                    organization.Id,
                    new SaveBranchRequest("Blocked", null, null, "UTC", false)));

        Assert.Equal("branch_limit_reached", exception.Code);
    }

    private static PlatformBranchService CreateService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IAuditService auditService) =>
        new(unitOfWork, currentUser, auditService, new SaveBranchValidator());

    private sealed class TestCurrentUser : ICurrentUserContext
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid? UserId { get; init; }
        public Guid? PlatformUserId { get; init; }
        public Guid? OrganizationId { get; init; }
        public OrganizationRole? OrganizationRole { get; init; }
        public PlatformRole? PlatformRole { get; init; }
        public string? IpAddress => null;
        public string? UserAgent => null;

        public static TestCurrentUser PlatformAdministrator() =>
            new()
            {
                PlatformUserId = Guid.NewGuid(),
                PlatformRole = OpsManager.Domain.Enums.PlatformRole.Administrator,
            };

        public static TestCurrentUser OrganizationManager(Guid organizationId) =>
            new()
            {
                UserId = Guid.NewGuid(),
                OrganizationId = organizationId,
                OrganizationRole = OpsManager.Domain.Enums.OrganizationRole.Manager,
            };
    }

    private sealed class RecordingAuditService : IAuditService
    {
        public List<string> PlatformActions { get; } = [];

        public Task RecordTenantAsync(
            Guid organizationId,
            string action,
            string entityType,
            Guid? entityId,
            IReadOnlyDictionary<string, string>? oldValues = null,
            IReadOnlyDictionary<string, string>? newValues = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RecordPlatformAsync(
            string action,
            string entityType,
            Guid? entityId,
            Guid? organizationId = null,
            IReadOnlyDictionary<string, string>? oldValues = null,
            IReadOnlyDictionary<string, string>? newValues = null,
            CancellationToken cancellationToken = default)
        {
            PlatformActions.Add(action);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        private readonly Dictionary<Type, object> _repositories = [];

        public IGenericRepository<TEntity> Repository<TEntity>()
            where TEntity : BaseEntity
        {
            if (!_repositories.TryGetValue(typeof(TEntity), out object? repository))
            {
                repository = new InMemoryRepository<TEntity>();
                _repositories.Add(typeof(TEntity), repository);
            }

            return (IGenericRepository<TEntity>)repository;
        }

        public void Seed(params BaseEntity[] entities)
        {
            foreach (BaseEntity entity in entities)
            {
                GetType()
                    .GetMethod(nameof(SeedOne), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .MakeGenericMethod(entity.GetType())
                    .Invoke(this, [entity]);
            }
        }

        public List<TEntity> Items<TEntity>()
            where TEntity : BaseEntity =>
            ((InMemoryRepository<TEntity>)Repository<TEntity>()).Items;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task ExecuteWithStrategyAsync(Func<Task> operation) =>
            operation();

        public Task<TResult> ExecuteWithStrategyAsync<TResult>(Func<Task<TResult>> operation) =>
            operation();

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private void SeedOne<TEntity>(TEntity entity)
            where TEntity : BaseEntity =>
            ((InMemoryRepository<TEntity>)Repository<TEntity>()).Items.Add(entity);
    }

    private sealed class InMemoryRepository<TEntity> : IGenericRepository<TEntity>
        where TEntity : BaseEntity
    {
        public List<TEntity> Items { get; } = [];

        public Task<TEntity?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(entity => entity.Id == id));

        public Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(predicate.Compile()));

        public Task<PagedResult<TEntity>> ListAsync(
            Expression<Func<TEntity, bool>>? predicate,
            PageRequest page,
            CancellationToken cancellationToken = default)
        {
            PageRequest validated = page.Validate();
            IEnumerable<TEntity> filtered =
                predicate is null ? Items : Items.Where(predicate.Compile());
            List<TEntity> all = filtered.ToList();
            return Task.FromResult(
                new PagedResult<TEntity>(
                    all.Skip(validated.Skip).Take(validated.PageSize).ToArray(),
                    validated.Page,
                    validated.PageSize,
                    all.Count));
        }

        public Task<IReadOnlyList<TResult>> ProjectAsync<TResult>(
            Expression<Func<TEntity, bool>>? predicate,
            Expression<Func<TEntity, TResult>> selector,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<TEntity> filtered =
                predicate is null ? Items : Items.Where(predicate.Compile());
            return Task.FromResult<IReadOnlyList<TResult>>(
                filtered.Select(selector.Compile()).ToArray());
        }

        public Task<IReadOnlyList<TResult>> ProjectJoinAsync<TOther, TKey, TResult>(
            Expression<Func<TEntity, bool>>? predicate,
            Expression<Func<TOther, bool>>? otherPredicate,
            Expression<Func<TEntity, TKey>> keySelector,
            Expression<Func<TOther, TKey>> otherKeySelector,
            Expression<Func<TEntity, TOther, TResult>> selector,
            CancellationToken cancellationToken = default)
            where TOther : BaseEntity =>
            throw new NotSupportedException();

        public Task<int> CountAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                predicate is null ? Items.Count : Items.Count(predicate.Compile()));

        public Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Any(predicate.Compile()));

        public Task AddAsync(
            TEntity entity,
            CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(
            IEnumerable<TEntity> entities,
            CancellationToken cancellationToken = default)
        {
            Items.AddRange(entities);
            return Task.CompletedTask;
        }

        public void Update(TEntity entity)
        {
        }

        public void Remove(TEntity entity) => Items.Remove(entity);

        public void DeletePermanently(TEntity entity) => Items.Remove(entity);
    }
}
