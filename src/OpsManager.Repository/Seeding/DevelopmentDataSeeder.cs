using Microsoft.EntityFrameworkCore;
using OpsManager.Domain.Constants;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Repository.Persistence;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Repository.Seeding;

public interface IDevelopmentDataSeeder
{
    Task SeedAsync(string passwordHash, CancellationToken cancellationToken = default);
}

public sealed class DevelopmentDataSeeder(OpsManagerDbContext context) : IDevelopmentDataSeeder
{
    private static readonly Guid PlatformAdministratorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PlanId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid OrganizationId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid BranchId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid KitchenDepartmentId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid ServiceDepartmentId = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid ManagementDepartmentId = Guid.Parse("50000000-0000-0000-0000-000000000003");
    private static readonly Guid ManagerId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid SupervisorId = Guid.Parse("60000000-0000-0000-0000-000000000002");
    private static readonly Guid EmployeeId = Guid.Parse("60000000-0000-0000-0000-000000000003");
    private static readonly Guid SubscriptionId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid TaskTemplateId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid TaskTemplateItemOneId = Guid.Parse("80000000-0000-0000-0000-000000000002");
    private static readonly Guid TaskTemplateItemTwoId = Guid.Parse("80000000-0000-0000-0000-000000000003");
    private static readonly Guid OrderTemplateId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static readonly Guid OrderTemplateItemOneId = Guid.Parse("90000000-0000-0000-0000-000000000002");
    private static readonly Guid OrderTemplateItemTwoId = Guid.Parse("90000000-0000-0000-0000-000000000003");

    public async Task SeedAsync(string passwordHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("A generated password hash is required.", nameof(passwordHash));
        }

        await context.Database.MigrateAsync(cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await AddIfMissingAsync(new PlatformUser(
            "Development Platform Administrator",
            "platform.admin@opsmanager.local",
            passwordHash,
            PlatformRole.Administrator,
            SupportedLanguages.English)
        {
            Id = PlatformAdministratorId,
        }, cancellationToken);

        await AddIfMissingAsync(new SubscriptionPlan
        {
            Id = PlanId,
            Name = "Development Standard",
            Code = "development-standard",
            Description = "Development-only plan with all MVP modules enabled.",
            MonthlyPrice = 49m,
            YearlyPrice = 490m,
            Currency = "USD",
            MaxUsers = 100,
            MaxBranches = 10,
            MaxStorageMb = 10240,
            Features = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SubscriptionFeatureKeys.Tasks] = "true",
                [SubscriptionFeatureKeys.DepartmentOrders] = "true",
                [SubscriptionFeatureKeys.Complaints] = "true",
                [SubscriptionFeatureKeys.Reports] = "true",
            },
            GracePeriodDays = 7,
        }, cancellationToken);

        await AddIfMissingAsync(new Organization("Sample Organization", "UTC", SupportedLanguages.English)
        {
            Id = OrganizationId,
            LegalName = "Sample Organization Development LLC",
        }, cancellationToken);

        await AddIfMissingAsync(new User("Development Manager", "manager@opsmanager.local", passwordHash, SupportedLanguages.English)
        {
            Id = ManagerId,
        }, cancellationToken);
        await AddIfMissingAsync(new User("Development Supervisor", "supervisor@opsmanager.local", passwordHash, SupportedLanguages.English)
        {
            Id = SupervisorId,
        }, cancellationToken);
        await AddIfMissingAsync(new User("Development Employee", "employee@opsmanager.local", passwordHash, SupportedLanguages.English)
        {
            Id = EmployeeId,
        }, cancellationToken);

        await AddIfMissingAsync(new Branch(OrganizationId, "Main Branch", "UTC")
        {
            Id = BranchId,
            IsPrimary = true,
        }, cancellationToken);

        await AddIfMissingAsync(new Department(OrganizationId, BranchId, "Kitchen")
        {
            Id = KitchenDepartmentId,
            SupervisorUserId = SupervisorId,
        }, cancellationToken);
        await AddIfMissingAsync(new Department(OrganizationId, BranchId, "Service")
        {
            Id = ServiceDepartmentId,
        }, cancellationToken);
        await AddIfMissingAsync(new Department(OrganizationId, BranchId, "Management")
        {
            Id = ManagementDepartmentId,
            SupervisorUserId = ManagerId,
        }, cancellationToken);

        await AddIfMissingAsync(new OrganizationMember(OrganizationId, ManagerId, OrganizationRole.Manager, now)
        {
            Id = Guid.Parse("61000000-0000-0000-0000-000000000001"),
        }, cancellationToken);
        await AddIfMissingAsync(new OrganizationMember(OrganizationId, SupervisorId, OrganizationRole.Supervisor, now)
        {
            Id = Guid.Parse("61000000-0000-0000-0000-000000000002"),
        }, cancellationToken);
        await AddIfMissingAsync(new OrganizationMember(OrganizationId, EmployeeId, OrganizationRole.Employee, now)
        {
            Id = Guid.Parse("61000000-0000-0000-0000-000000000003"),
        }, cancellationToken);

        await AddIfMissingAsync(new UserDepartment(OrganizationId, ManagerId, ManagementDepartmentId, now)
        {
            Id = Guid.Parse("62000000-0000-0000-0000-000000000001"),
            IsPrimary = true,
        }, cancellationToken);
        await AddIfMissingAsync(new UserDepartment(OrganizationId, SupervisorId, KitchenDepartmentId, now)
        {
            Id = Guid.Parse("62000000-0000-0000-0000-000000000002"),
            IsPrimary = true,
        }, cancellationToken);
        await AddIfMissingAsync(new UserDepartment(OrganizationId, EmployeeId, ServiceDepartmentId, now)
        {
            Id = Guid.Parse("62000000-0000-0000-0000-000000000003"),
            IsPrimary = true,
        }, cancellationToken);

        await AddIfMissingAsync(new OrganizationSubscription
        {
            Id = SubscriptionId,
            OrganizationId = OrganizationId,
            PlanId = PlanId,
            Status = SubscriptionStatus.Trial,
            BillingMode = BillingMode.Trial,
            TrialStartedAt = now,
            TrialEndsAt = now.AddDays(14),
            GracePeriodEndsAt = now.AddDays(21),
        }, cancellationToken);

        await AddIfMissingAsync(new TaskTemplate(OrganizationId, KitchenDepartmentId, "Opening checklist", ManagerId)
        {
            Id = TaskTemplateId,
            BranchId = BranchId,
            DefaultAssigneeUserId = SupervisorId,
            Description = "Snapshot source for a sample scheduled operational task.",
            RequiresApproval = true,
        }, cancellationToken);
        await AddIfMissingAsync(new TaskTemplateItem(OrganizationId, TaskTemplateId, "Inspect preparation area", 1)
        {
            Id = TaskTemplateItemOneId,
            IsRequired = true,
            EvidenceMode = EvidenceMode.Required,
        }, cancellationToken);
        await AddIfMissingAsync(new TaskTemplateItem(OrganizationId, TaskTemplateId, "Verify equipment status", 2)
        {
            Id = TaskTemplateItemTwoId,
            IsRequired = true,
            EvidenceMode = EvidenceMode.Optional,
        }, cancellationToken);

        await AddIfMissingAsync(new OrderTemplate(
            OrganizationId,
            BranchId,
            "Service supplies request",
            ServiceDepartmentId,
            KitchenDepartmentId,
            ManagerId)
        {
            Id = OrderTemplateId,
        }, cancellationToken);
        await AddIfMissingAsync(new OrderTemplateItem
        {
            Id = OrderTemplateItemOneId,
            OrganizationId = OrganizationId,
            OrderTemplateId = OrderTemplateId,
            Name = "Drinking water",
            UnitCode = UnitCode.Liter,
            DefaultQuantity = 10m,
            SortOrder = 1,
        }, cancellationToken);
        await AddIfMissingAsync(new OrderTemplateItem
        {
            Id = OrderTemplateItemTwoId,
            OrganizationId = OrganizationId,
            OrderTemplateId = OrderTemplateId,
            Name = "Service napkins",
            UnitCode = UnitCode.Package,
            DefaultQuantity = 2m,
            SortOrder = 2,
        }, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task AddIfMissingAsync<TEntity>(TEntity entity, CancellationToken cancellationToken)
        where TEntity : Domain.Common.BaseEntity
    {
        bool exists = await context.Set<TEntity>()
            .IgnoreQueryFilters()
            .AnyAsync(existing => existing.Id == entity.Id, cancellationToken);
        if (!exists)
        {
            await context.AddAsync(entity, cancellationToken);
        }
    }
}
