using Microsoft.EntityFrameworkCore;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Repository.IntegrationTests.Infrastructure;
using OpsManager.Repository.Persistence;
using OpsManager.Repository.Repositories;
using OpsManager.Repository.Seeding;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Repository.IntegrationTests;

public sealed class PersistenceIntegrationTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [DockerFact]
    public async Task Migration_applies_and_enum_and_jsonb_values_round_trip()
    {
        await using OpsManagerDbContext context = fixture.CreateContext(new TestTenantContext(null, true));
        SubscriptionPlan plan = new()
        {
            Name = "JSON test plan",
            Code = $"json-{Guid.NewGuid():N}",
            Currency = "USD",
            MaxUsers = 10,
            MaxBranches = 2,
            MaxStorageMb = 100,
            Features = new Dictionary<string, string> { ["tasks"] = "true" },
        };
        Organization organization = new($"Organization {Guid.NewGuid():N}", "UTC", "en")
        {
            Status = OrganizationStatus.Suspended,
        };
        context.AddRange(plan, organization);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        SubscriptionPlan storedPlan = await context.SubscriptionPlans.SingleAsync(entity => entity.Id == plan.Id);
        Organization storedOrganization = await context.Organizations.SingleAsync(entity => entity.Id == organization.Id);

        Assert.Equal("true", storedPlan.Features["tasks"]);
        Assert.Equal(OrganizationStatus.Suspended, storedOrganization.Status);
    }

    [DockerFact]
    public async Task Important_membership_unique_constraint_is_enforced()
    {
        await using OpsManagerDbContext context = fixture.CreateContext(new TestTenantContext(null, true));
        Organization organization = new($"Organization {Guid.NewGuid():N}", "UTC", "en");
        User user = new("Unique member", $"{Guid.NewGuid():N}@example.test", "hash", "en");
        context.AddRange(organization, user);
        await context.SaveChangesAsync();

        context.AddRange(
            new OrganizationMember(organization.Id, user.Id, OrganizationRole.Manager, DateTimeOffset.UtcNow),
            new OrganizationMember(organization.Id, user.Id, OrganizationRole.Employee, DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [DockerFact]
    public async Task Repository_enforces_tenant_boundary_pagination_and_soft_delete()
    {
        Guid firstOrganizationId;
        Guid secondOrganizationId;
        Guid secondTenantBranchId;
        Branch firstBranch;
        await using (OpsManagerDbContext setup = fixture.CreateContext(new TestTenantContext(null, true)))
        {
            Organization firstOrganization = new($"Organization {Guid.NewGuid():N}", "UTC", "en");
            Organization secondOrganization = new($"Organization {Guid.NewGuid():N}", "UTC", "en");
            firstOrganizationId = firstOrganization.Id;
            secondOrganizationId = secondOrganization.Id;
            firstBranch = new Branch(firstOrganizationId, "Alpha", "UTC");
            Branch secondTenantBranch = new(secondOrganizationId, "Other tenant", "UTC");
            secondTenantBranchId = secondTenantBranch.Id;
            setup.AddRange(
                firstOrganization,
                secondOrganization,
                firstBranch,
                new Branch(firstOrganizationId, "Beta", "UTC"),
                secondTenantBranch);
            await setup.SaveChangesAsync();
        }

        await using OpsManagerDbContext context = fixture.CreateContext(new TestTenantContext(firstOrganizationId, false));
        UnitOfWork unitOfWork = new(context);
        IGenericRepository<Branch> repository = unitOfWork.Repository<Branch>();

        PagedResult<Branch> page = await repository.ListAsync(null, new PageRequest(1, 1));

        Assert.Single(page.Items);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, await repository.CountAsync());
        Assert.True(await repository.AnyAsync(branch => branch.Name == "Beta"));
        Assert.Null(await repository.GetByIdAsync(secondTenantBranchId));

        Branch trackedBranch = await context.Branches.SingleAsync(branch => branch.Id == firstBranch.Id);
        repository.Remove(trackedBranch);
        await unitOfWork.SaveChangesAsync();

        Assert.Equal(1, await repository.CountAsync());

        await using OpsManagerDbContext secondTenant = fixture.CreateContext(new TestTenantContext(secondOrganizationId, false));
        Assert.Single(await secondTenant.Branches.AsNoTracking().ToListAsync());
    }

    [DockerFact]
    public async Task UnitOfWork_transaction_can_roll_back_an_atomic_operation()
    {
        Organization organization = new($"Organization {Guid.NewGuid():N}", "UTC", "en");
        await using (OpsManagerDbContext setup = fixture.CreateContext(new TestTenantContext(null, true)))
        {
            setup.Add(organization);
            await setup.SaveChangesAsync();
        }

        Guid branchId = Guid.NewGuid();
        await using (OpsManagerDbContext context = fixture.CreateContext(new TestTenantContext(organization.Id, false)))
        {
            UnitOfWork unitOfWork = new(context);
            await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync();
            await unitOfWork.Repository<Branch>().AddAsync(new Branch(organization.Id, "Rolled back", "UTC") { Id = branchId });
            await unitOfWork.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using OpsManagerDbContext verification = fixture.CreateContext(new TestTenantContext(organization.Id, false));
        Assert.False(await verification.Branches.AnyAsync(branch => branch.Id == branchId));
    }

    [DockerFact]
    public async Task Order_item_snapshot_fields_persist_unchanged()
    {
        await using OpsManagerDbContext context = fixture.CreateContext(new TestTenantContext(null, true));
        Organization organization = new($"Organization {Guid.NewGuid():N}", "UTC", "en");
        Branch branch = new(organization.Id, "Snapshot branch", "UTC");
        Department source = new(organization.Id, branch.Id, "Source");
        Department target = new(organization.Id, branch.Id, "Target");
        User creator = new("Snapshot creator", $"{Guid.NewGuid():N}@example.test", "hash", "en");
        DepartmentOrder order = new(
            organization.Id,
            branch.Id,
            $"ORD-{Guid.NewGuid():N}",
            source.Id,
            target.Id,
            creator.Id,
            DateTimeOffset.UtcNow);
        DepartmentOrderItem item = new()
        {
            OrganizationId = organization.Id,
            DepartmentOrderId = order.Id,
            ItemNameSnapshot = "Original item name",
            ItemDescriptionSnapshot = "Original description",
            UnitCodeSnapshot = UnitCode.Kilogram,
            RequestedQuantity = 2.5m,
        };
        context.AddRange(organization, branch, source, target, creator, order, item);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        DepartmentOrderItem stored = await context.DepartmentOrderItems.SingleAsync(entity => entity.Id == item.Id);

        Assert.Equal("Original item name", stored.ItemNameSnapshot);
        Assert.Equal("Original description", stored.ItemDescriptionSnapshot);
        Assert.Equal(2.5m, stored.RequestedQuantity);
    }

    [DockerFact]
    public async Task Development_seed_is_idempotent()
    {
        await using OpsManagerDbContext context = fixture.CreateContext(new TestTenantContext(null, true));
        DevelopmentDataSeeder seeder = new(context);

        await seeder.SeedAsync("development-test-hash");
        await seeder.SeedAsync("development-test-hash");

        Assert.Equal(1, await context.PlatformUsers.CountAsync(entity => entity.Email == "platform.admin@opsmanager.local"));
        Assert.Equal(1, await context.Organizations.CountAsync(entity => entity.Name == "Sample Organization"));
        Assert.Equal(3, await context.OrganizationMembers.CountAsync(entity => entity.OrganizationId == Guid.Parse("30000000-0000-0000-0000-000000000001")));
    }
}
