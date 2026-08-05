using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Repository.IntegrationTests.Infrastructure;
using OpsManager.Repository.Persistence;
using OpsManager.Repository.Repositories;
using OpsManager.Repository.Seeding;
using Testcontainers.PostgreSql;
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

    [DockerFact]
    public async Task Xmin_concurrency_rejects_a_stale_operational_task_update()
    {
        Organization organization = new($"Concurrency {Guid.NewGuid():N}", "UTC", "en");
        Branch branch = new(organization.Id, "Concurrency branch", "UTC");
        Department department = new(organization.Id, branch.Id, "Concurrency department");
        User creator = new("Concurrency user", $"{Guid.NewGuid():N}@example.test", "hash", "en");
        DateTimeOffset start = DateTimeOffset.UtcNow;
        OperationalTask task = new(
            organization.Id,
            branch.Id,
            department.Id,
            "Original",
            DateOnly.FromDateTime(start.UtcDateTime),
            start,
            start.AddHours(1),
            creator.Id);
        await using (OpsManagerDbContext setup = fixture.CreateContext(new TestTenantContext(null, true)))
        {
            setup.AddRange(organization, branch, department, creator, task);
            await setup.SaveChangesAsync();
        }

        await using OpsManagerDbContext firstContext = fixture.CreateContext(new TestTenantContext(organization.Id, false));
        await using OpsManagerDbContext secondContext = fixture.CreateContext(new TestTenantContext(organization.Id, false));
        UnitOfWork first = new(firstContext);
        UnitOfWork second = new(secondContext);
        OperationalTask firstCopy = (await first.Repository<OperationalTask>().GetByIdAsync(task.Id))!;
        OperationalTask staleCopy = (await second.Repository<OperationalTask>().GetByIdAsync(task.Id))!;
        firstCopy.UpdateDetails("First update", null, TaskPriority.Normal, false);
        staleCopy.UpdateDetails("Stale update", null, TaskPriority.Normal, false);
        first.Repository<OperationalTask>().Update(firstCopy);
        second.Repository<OperationalTask>().Update(staleCopy);

        await first.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [DockerFact]
    public async Task Scheduled_distribution_and_per_user_copy_keys_are_unique()
    {
        await using OpsManagerDbContext context = fixture.CreateContext(new TestTenantContext(null, true));
        Organization organization = new($"Occurrence {Guid.NewGuid():N}", "UTC", "en");
        Branch branch = new(organization.Id, "Occurrence branch", "UTC");
        Department department = new(organization.Id, branch.Id, "Occurrence department");
        User creator = new("Occurrence creator", $"{Guid.NewGuid():N}@example.test", "hash", "en");
        User firstUser = new("Occurrence first", $"{Guid.NewGuid():N}@example.test", "hash", "en");
        User secondUser = new("Occurrence second", $"{Guid.NewGuid():N}@example.test", "hash", "en");
        TaskTemplate template = new(organization.Id, department.Id, "Occurrence template", creator.Id);
        DateOnly date = new(2030, 1, 1);
        TaskSchedule schedule = new(
            organization.Id,
            template.Id,
            branch.Id,
            department.Id,
            TaskAssignmentMode.SingleUser,
            RecurrenceType.Daily,
            date,
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            creator.Id);
        DateTimeOffset firstStart = new(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
        TaskDistribution firstDistribution = Distribution(firstStart);
        TaskDistribution secondDistribution = Distribution(firstStart.AddHours(1));
        OperationalTask first = Scheduled(firstDistribution, firstUser.Id, firstStart);
        OperationalTask second = Scheduled(firstDistribution, secondUser.Id, firstStart);
        context.AddRange(
            organization,
            branch,
            department,
            creator,
            firstUser,
            secondUser,
            template,
            schedule,
            firstDistribution,
            secondDistribution,
            first,
            second);

        await context.SaveChangesAsync();

        context.Add(Scheduled(firstDistribution, firstUser.Id, firstStart));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        TaskDistribution Distribution(DateTimeOffset scheduledStart) =>
            new(
                organization.Id,
                branch.Id,
                department.Id,
                TaskAssignmentMode.SelectedUsers,
                date,
                scheduledStart,
                scheduledStart.AddMinutes(30),
                creator.Id,
                template.Id,
                schedule.Id);

        OperationalTask Scheduled(
            TaskDistribution distribution,
            Guid assigneeUserId,
            DateTimeOffset scheduledStart) =>
            OperationalTask.CreateAssignedCopy(
                organization.Id,
                distribution.Id,
                branch.Id,
                department.Id,
                assigneeUserId,
                template.Id,
                schedule.Id,
                null,
                "Occurrence",
                null,
                date,
                scheduledStart,
                scheduledStart.AddMinutes(30),
                TaskPriority.Normal,
                false,
                creator.Id);
    }

    [DockerFact]
    public async Task Schedule_weekdays_and_history_event_time_round_trip_independently()
    {
        await using OpsManagerDbContext context = fixture.CreateContext(new TestTenantContext(null, true));
        Organization organization = new($"Schedule history {Guid.NewGuid():N}", "UTC", "en");
        Branch branch = new(organization.Id, "Schedule branch", "UTC");
        Department department = new(organization.Id, branch.Id, "Schedule department");
        User creator = new("Schedule creator", $"{Guid.NewGuid():N}@example.test", "hash", "en");
        TaskTemplate template = new(organization.Id, department.Id, "Schedule template", creator.Id);
        DateOnly date = new(2031, 1, 6);
        TaskSchedule schedule = new(
            organization.Id,
            template.Id,
            branch.Id,
            department.Id,
            TaskAssignmentMode.SingleUser,
            RecurrenceType.Weekly,
            date,
            new TimeOnly(9, 0),
            new TimeOnly(10, 0),
            creator.Id,
            [Weekday.Friday, Weekday.Monday, Weekday.Friday]);
        DateTimeOffset start = new(2031, 1, 6, 9, 0, 0, TimeSpan.Zero);
        OperationalTask task = new(
            organization.Id,
            branch.Id,
            department.Id,
            "History task",
            date,
            start,
            start.AddHours(1),
            creator.Id);
        DateTimeOffset occurredAt = new(2029, 4, 3, 2, 1, 0, TimeSpan.Zero);
        TaskStatusHistory history = TaskStatusHistory.Created(
            organization.Id,
            task.Id,
            creator.Id,
            occurredAt);
        context.AddRange(organization, branch, department, creator, template, schedule, task, history);

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        TaskSchedule storedSchedule = await context.TaskSchedules.SingleAsync(entity => entity.Id == schedule.Id);
        TaskStatusHistory storedHistory = await context.TaskStatusHistories.SingleAsync(entity => entity.Id == history.Id);

        Assert.Equal([Weekday.Monday, Weekday.Friday], storedSchedule.Weekdays);
        Assert.Equal(occurredAt, storedHistory.OccurredAt);
        Assert.NotEqual(storedHistory.CreatedAt, storedHistory.OccurredAt);
        Assert.False(await ColumnExistsAsync(context, "task_schedules", "recurrence_rule"));
    }

    [DockerFact]
    public async Task Refinement_migration_backfills_event_time_and_rolls_back_without_deleting_history()
    {
        const string PreviousMigration = "20260726225536_RefactorTaskDomainModel";
        await using PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("opsmanager_migration_tests")
            .WithUsername("postgres")
            .WithPassword("postgres_tests_only")
            .Build();
        await container.StartAsync();

        DbContextOptions<OpsManagerDbContext> options = new DbContextOptionsBuilder<OpsManagerDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;
        await using OpsManagerDbContext context = new(options, new TestTenantContext(null, true));
        IMigrator migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        Organization organization = new($"Migration history {Guid.NewGuid():N}", "UTC", "en");
        Branch branch = new(organization.Id, "Migration branch", "UTC");
        Department department = new(organization.Id, branch.Id, "Migration department");
        User creator = new("Migration creator", $"{Guid.NewGuid():N}@example.test", "hash", "en");
        DateTimeOffset start = new(2032, 2, 2, 8, 0, 0, TimeSpan.Zero);
        context.AddRange(organization, branch, department, creator);
        await context.SaveChangesAsync();

        Guid taskId = Guid.NewGuid();
        Guid unassignedTaskId = Guid.NewGuid();
        Guid historyId = Guid.NewGuid();
        DateTimeOffset originalCreatedAt = new(2028, 8, 7, 6, 5, 4, TimeSpan.Zero);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO tasks
                 (id, organization_id, branch_id, department_id, assignee_user_id, title,
                  occurrence_date, scheduled_start_at, due_at, priority, status,
                  requires_approval, is_schedule_override, created_by, created_at, updated_at)
             VALUES
                 ({taskId}, {organization.Id}, {branch.Id}, {department.Id}, {creator.Id}, {"Assigned migration task"},
                  {DateOnly.FromDateTime(start.UtcDateTime)}, {start}, {start.AddHours(1)}, {"Normal"}, {"NotStarted"},
                  {false}, {false}, {creator.Id}, {originalCreatedAt}, {originalCreatedAt}),
                 ({unassignedTaskId}, {organization.Id}, {branch.Id}, {department.Id}, {null}, {"Unassigned migration task"},
                  {DateOnly.FromDateTime(start.UtcDateTime)}, {start.AddHours(2)}, {start.AddHours(3)}, {"Normal"}, {"NotStarted"},
                  {false}, {false}, {creator.Id}, {originalCreatedAt}, {originalCreatedAt});

             INSERT INTO task_status_history
                 (id, organization_id, task_id, old_status, new_status, changed_by, reason, created_at, updated_at)
             VALUES
                 ({historyId}, {organization.Id}, {taskId}, {null}, {"NotStarted"}, {creator.Id}, {"Migrated history"}, {originalCreatedAt}, {originalCreatedAt});
             """);

        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();

        TaskStatusHistory storedHistory = await context.TaskStatusHistories
            .IgnoreQueryFilters()
            .SingleAsync(entity => entity.Id == historyId);
        Assert.Equal(originalCreatedAt, storedHistory.CreatedAt);
        Assert.Equal(originalCreatedAt, storedHistory.OccurredAt);
        OperationalTask assignedTask = await context.Tasks.IgnoreQueryFilters().SingleAsync(entity => entity.Id == taskId);
        OperationalTask unassignedTask = await context.Tasks.IgnoreQueryFilters().SingleAsync(entity => entity.Id == unassignedTaskId);
        Assert.Equal(taskId, assignedTask.TaskDistributionId);
        Assert.Null(unassignedTask.TaskDistributionId);
        Assert.Null(unassignedTask.AssigneeUserId);
        Assert.False(await ColumnExistsAsync(context, "task_schedules", "recurrence_rule"));
        Assert.True(await ColumnExistsAsync(context, "task_status_history", "occurred_at"));

        await migrator.MigrateAsync(PreviousMigration);

        Assert.True(await ColumnExistsAsync(context, "task_schedules", "recurrence_rule"));
        Assert.False(await ColumnExistsAsync(context, "task_status_history", "occurred_at"));
        Assert.Equal(
            1,
            await context.Database.SqlQueryRaw<int>(
                    """SELECT COUNT(*)::int AS "Value" FROM task_status_history WHERE id = {0}""",
                    historyId)
                .SingleAsync());
    }

    private static async Task<bool> ColumnExistsAsync(
        OpsManagerDbContext context,
        string tableName,
        string columnName)
    {
        DbConnection connection = context.Database.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = @table_name
                      AND column_name = @column_name);
                """;
            DbParameter tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "table_name";
            tableParameter.Value = tableName;
            command.Parameters.Add(tableParameter);
            DbParameter columnParameter = command.CreateParameter();
            columnParameter.ParameterName = "column_name";
            columnParameter.Value = columnName;
            command.Parameters.Add(columnParameter);
            return (bool)(await command.ExecuteScalarAsync())!;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
