using System.Linq.Expressions;
using OpsManager.Domain.Common;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Tasks;
using OpsManager.Service.Tasks.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Tests;

public sealed class TaskAssignmentTests
{
    [Fact]
    public async Task Resolver_returns_only_active_employee_members_of_the_selected_department()
    {
        Guid organizationId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();
        Department department = new(organizationId, branchId, "Operations");
        User eligible = User("Eligible");
        User suspended = User("Suspended");
        suspended.AccountStatus = UserAccountStatus.Suspended;
        User manager = User("Manager");
        User otherDepartment = User("Other");
        MemoryUnitOfWork unitOfWork = new(
            [department],
            [eligible, suspended, manager, otherDepartment],
            [
                new OrganizationMember(organizationId, eligible.Id, OrganizationRole.Employee, DateTimeOffset.UtcNow),
                new OrganizationMember(organizationId, suspended.Id, OrganizationRole.Employee, DateTimeOffset.UtcNow),
                new OrganizationMember(organizationId, manager.Id, OrganizationRole.Manager, DateTimeOffset.UtcNow),
                new OrganizationMember(organizationId, otherDepartment.Id, OrganizationRole.Employee, DateTimeOffset.UtcNow),
            ],
            [
                new UserDepartment(organizationId, eligible.Id, department.Id, DateTimeOffset.UtcNow),
                new UserDepartment(organizationId, suspended.Id, department.Id, DateTimeOffset.UtcNow),
                new UserDepartment(organizationId, manager.Id, department.Id, DateTimeOffset.UtcNow),
                new UserDepartment(organizationId, otherDepartment.Id, Guid.NewGuid(), DateTimeOffset.UtcNow),
            ]);
        TaskAssigneeResolver resolver = new(unitOfWork, new TestCurrentUser(Guid.NewGuid(), organizationId, OrganizationRole.Manager));

        IReadOnlyList<ResolvedTaskAssignee> result = await resolver.ResolveAsync(
            organizationId,
            branchId,
            department.Id,
            TaskAssignmentMode.AllDepartmentMembers,
            []);

        ResolvedTaskAssignee assignee = Assert.Single(result);
        Assert.Equal(eligible.Id, assignee.UserId);
    }

    [Fact]
    public async Task Resolver_rejects_duplicate_and_out_of_department_selected_users()
    {
        Guid organizationId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();
        Department department = new(organizationId, branchId, "Operations");
        User first = User("First");
        User second = User("Second");
        MemoryUnitOfWork unitOfWork = new(
            [department],
            [first, second],
            [
                new OrganizationMember(organizationId, first.Id, OrganizationRole.Employee, DateTimeOffset.UtcNow),
                new OrganizationMember(organizationId, second.Id, OrganizationRole.Employee, DateTimeOffset.UtcNow),
            ],
            [new UserDepartment(organizationId, first.Id, department.Id, DateTimeOffset.UtcNow)]);
        TaskAssigneeResolver resolver = new(unitOfWork, new TestCurrentUser(Guid.NewGuid(), organizationId, OrganizationRole.Manager));

        await Assert.ThrowsAsync<RequestValidationException>(() => resolver.ResolveAsync(
            organizationId,
            branchId,
            department.Id,
            TaskAssignmentMode.SelectedUsers,
            [first.Id, first.Id]));
        await Assert.ThrowsAsync<RequestValidationException>(() => resolver.ResolveAsync(
            organizationId,
            branchId,
            department.Id,
            TaskAssignmentMode.SelectedUsers,
            [first.Id, second.Id]));

        IReadOnlyList<ResolvedTaskAssignee> scheduled = await resolver.ResolveScheduledAsync(
            organizationId,
            branchId,
            department.Id,
            TaskAssignmentMode.SelectedUsers,
            [first.Id, second.Id]);
        Assert.Equal(first.Id, Assert.Single(scheduled).UserId);
    }

    [Fact]
    public async Task Supervisor_cannot_assign_tasks_to_supervisors_but_manager_can()
    {
        Guid organizationId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();
        Department department = new(organizationId, branchId, "Operations");
        User employeeUser = User("Employee User");
        User supervisorUser = User("Supervisor User");
        MemoryUnitOfWork unitOfWork = new(
            [department],
            [employeeUser, supervisorUser],
            [
                new OrganizationMember(organizationId, employeeUser.Id, OrganizationRole.Employee, DateTimeOffset.UtcNow),
                new OrganizationMember(organizationId, supervisorUser.Id, OrganizationRole.Supervisor, DateTimeOffset.UtcNow),
            ],
            [
                new UserDepartment(organizationId, employeeUser.Id, department.Id, DateTimeOffset.UtcNow),
                new UserDepartment(organizationId, supervisorUser.Id, department.Id, DateTimeOffset.UtcNow),
            ]);

        TaskAssigneeResolver supervisorResolver = new(unitOfWork, new TestCurrentUser(Guid.NewGuid(), organizationId, OrganizationRole.Supervisor));
        TaskAssigneeResolver managerResolver = new(unitOfWork, new TestCurrentUser(Guid.NewGuid(), organizationId, OrganizationRole.Manager));

        IReadOnlyList<ResolvedTaskAssignee> supervisorResults = await supervisorResolver.ResolveAsync(
            organizationId, branchId, department.Id, TaskAssignmentMode.AllDepartmentMembers, []);
        Assert.Single(supervisorResults);
        Assert.Equal(employeeUser.Id, supervisorResults[0].UserId);

        IReadOnlyList<ResolvedTaskAssignee> managerResults = await managerResolver.ResolveAsync(
            organizationId, branchId, department.Id, TaskAssignmentMode.AllDepartmentMembers, []);
        Assert.Equal(2, managerResults.Count);
        Assert.Contains(managerResults, a => a.UserId == employeeUser.Id);
        Assert.Contains(managerResults, a => a.UserId == supervisorUser.Id);
    }

    [Fact]
    public async Task Distribution_creator_builds_independent_tasks_items_histories_and_notifications()
    {
        MemoryUnitOfWork unitOfWork = new();
        RecordingNotificationService notifications = new();
        Guid organizationId = Guid.NewGuid();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        DateTimeOffset start = new(2035, 1, 1, 8, 0, 0, TimeSpan.Zero);
        TaskDistributionCreator creator = new(
            unitOfWork,
            notifications,
            new NoOpAuditService(),
            new FixedClock(start));

        TaskDistributionResponse result = await creator.CreateAsync(
            new TaskDistributionCreation(
                organizationId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                null,
                null,
                TaskAssignmentMode.SelectedUsers,
                [new(first, "First"), new(second, "Second")],
                "Safety check",
                null,
                DateOnly.FromDateTime(start.UtcDateTime),
                start,
                start.AddHours(1),
                TaskPriority.Normal,
                false,
                Guid.NewGuid(),
                [new(null, "Item", null, 0, true, EvidenceMode.None)]));

        Assert.Equal(2, result.CreatedTaskCount);
        OperationalTask[] tasks = unitOfWork.Items<OperationalTask>().ToArray();
        Assert.Equal(2, tasks.Length);
        Assert.Single(tasks.Select(task => task.TaskDistributionId).Distinct());
        Assert.Equal(2, tasks.Select(task => task.AssigneeUserId).Distinct().Count());
        Assert.Equal(2, unitOfWork.Items<TaskItem>().Count);
        Assert.Equal(2, unitOfWork.Items<TaskStatusHistory>().Count);
        Assert.Equal(2, notifications.Recipients.Count);
        Assert.True(unitOfWork.Transaction!.Committed);
    }

    [Fact]
    public async Task Distribution_creator_rolls_back_when_any_copy_cannot_be_created()
    {
        MemoryUnitOfWork unitOfWork = new();
        Guid organizationId = Guid.NewGuid();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        DateTimeOffset start = new(2035, 1, 1, 8, 0, 0, TimeSpan.Zero);
        TaskDistributionCreator creator = new(
            unitOfWork,
            new FailingNotificationService(second),
            new NoOpAuditService(),
            new FixedClock(start));

        await Assert.ThrowsAsync<InvalidOperationException>(() => creator.CreateAsync(
            new TaskDistributionCreation(
                organizationId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                null,
                null,
                TaskAssignmentMode.SelectedUsers,
                [new(first, "First"), new(second, "Second")],
                "Safety check",
                null,
                DateOnly.FromDateTime(start.UtcDateTime),
                start,
                start.AddHours(1),
                TaskPriority.Normal,
                false,
                Guid.NewGuid(),
                [])));

        Assert.NotNull(unitOfWork.Transaction);
        Assert.True(unitOfWork.Transaction.RolledBack);
        Assert.False(unitOfWork.Transaction.Committed);
    }

    [Fact]
    public async Task All_department_schedule_creates_one_copy_per_current_employee_and_is_idempotent()
    {
        Guid organizationId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        DateOnly occurrenceDate = new(2035, 1, 1);
        DateTimeOffset now = new(2035, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Branch branch = new(organizationId, "Main", "UTC");
        Department department = new(organizationId, branch.Id, "Operations");
        User first = User("First");
        User second = User("Second");
        TaskTemplate template = new(organizationId, department.Id, "Daily safety", managerId);
        TaskTemplateItem templateItem = new(
            organizationId,
            template.Id,
            "Inspect equipment",
            0,
            isRequired: true);
        TaskSchedule schedule = new(
            organizationId,
            template.Id,
            branch.Id,
            department.Id,
            TaskAssignmentMode.AllDepartmentMembers,
            RecurrenceType.Daily,
            occurrenceDate,
            new TimeOnly(8, 0),
            new TimeOnly(9, 0),
            managerId);
        MemoryUnitOfWork unitOfWork = new(
            [branch],
            [department],
            [first, second],
            [template],
            [templateItem],
            [schedule],
            [
                new OrganizationMember(organizationId, first.Id, OrganizationRole.Employee, now),
                new OrganizationMember(organizationId, second.Id, OrganizationRole.Employee, now),
            ],
            [
                new UserDepartment(organizationId, first.Id, department.Id, now),
                new UserDepartment(organizationId, second.Id, department.Id, now),
            ]);
        TaskAssigneeResolver resolver = new(unitOfWork, new TestCurrentUser(managerId, organizationId, OrganizationRole.Manager));
        TaskDistributionCreator creator = new(
            unitOfWork,
            new RecordingNotificationService(),
            new NoOpAuditService(),
            new FixedClock(now));
        TaskOccurrenceGeneratorService generator = new(
            unitOfWork,
            new TestCurrentUser(managerId, organizationId, OrganizationRole.Manager),
            new NoOpTenantScope(),
            new FixedClock(now),
            new SchedulerOptions(),
            resolver,
            creator);

        OccurrenceGenerationResult firstRun = await generator.GenerateAsync(schedule.Id, occurrenceDate);
        OccurrenceGenerationResult secondRun = await generator.GenerateAsync(schedule.Id, occurrenceDate);

        Assert.Equal(2, firstRun.CreatedCount);
        Assert.Equal(0, secondRun.CreatedCount);
        Assert.Single(unitOfWork.Items<TaskDistribution>());
        Assert.Equal(2, unitOfWork.Items<OperationalTask>().Count);
        Assert.Equal(2, unitOfWork.Items<TaskItem>().Count);
        Assert.Equal(
            2,
            unitOfWork.Items<OperationalTask>()
                .Select(task => task.AssigneeUserId)
                .Distinct()
                .Count());
    }

    [Fact]
    public async Task Employee_cannot_view_or_execute_another_users_task()
    {
        Guid organizationId = Guid.NewGuid();
        Guid ownerId = Guid.NewGuid();
        Guid otherEmployeeId = Guid.NewGuid();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        OperationalTask task = OperationalTask.CreateAssignedCopy(
            organizationId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ownerId,
            null,
            null,
            null,
            "Private task",
            null,
            DateOnly.FromDateTime(start.UtcDateTime),
            start,
            start.AddHours(1),
            TaskPriority.Normal,
            false,
            Guid.NewGuid());
        MemoryUnitOfWork unitOfWork = new([task]);
        TaskService service = Service(unitOfWork, new TestCurrentUser(
            otherEmployeeId,
            organizationId,
            OrganizationRole.Employee));

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetAsync(task.Id));
        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.StartAsync(task.Id));
        Assert.Equal(OperationalTaskStatus.NotStarted, task.Status);
    }

    [Fact]
    public async Task Manager_can_view_tenant_copies_but_cross_tenant_lookup_is_hidden()
    {
        Guid organizationId = Guid.NewGuid();
        Guid managerId = Guid.NewGuid();
        DateTimeOffset start = DateTimeOffset.UtcNow;
        OperationalTask tenantTask = OperationalTask.CreateAssignedCopy(
            organizationId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            "Tenant task",
            null,
            DateOnly.FromDateTime(start.UtcDateTime),
            start,
            start.AddHours(1),
            TaskPriority.Normal,
            false,
            managerId);
        OperationalTask otherTenantTask = OperationalTask.CreateAssignedCopy(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            "Other tenant task",
            null,
            DateOnly.FromDateTime(start.UtcDateTime),
            start,
            start.AddHours(1),
            TaskPriority.Normal,
            false,
            managerId);
        TaskService service = Service(
            new MemoryUnitOfWork([tenantTask, otherTenantTask]),
            new TestCurrentUser(managerId, organizationId, OrganizationRole.Manager));

        TaskDto visible = await service.GetAsync(tenantTask.Id);

        Assert.Equal(tenantTask.Id, visible.Id);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetAsync(otherTenantTask.Id));
    }

    private static TaskService Service(MemoryUnitOfWork unitOfWork, ICurrentUserContext currentUser) =>
        new(
            unitOfWork,
            currentUser,
            new AllowSubscriptionAccess(),
            new NoOpAuditService(),
            new RecordingNotificationService(),
            new NoOpFileStorage(),
            new FixedClock(DateTimeOffset.UtcNow),
            new CreateTaskValidator(),
            new TaskAssigneeResolver(unitOfWork, currentUser),
            new NoOpDistributionCreator());

    private static User User(string name) =>
        new(name, $"{Guid.NewGuid():N}@example.test", "hash", "en");

    private sealed class MemoryUnitOfWork(params object[][] groups) : IUnitOfWork
    {
        private readonly Dictionary<Type, object> _repositories = CreateRepositories(groups);

        public MemoryTransaction? Transaction { get; private set; }

        private static Dictionary<Type, object> CreateRepositories(object[][] source)
        {
            Dictionary<Type, object> repositories = [];
            foreach (IGrouping<Type, object> group in source.SelectMany(items => items).GroupBy(item => item.GetType()))
            {
                Array typedItems = Array.CreateInstance(group.Key, group.Count());
                int index = 0;
                foreach (object item in group)
                {
                    typedItems.SetValue(item, index++);
                }

                repositories[group.Key] = Activator.CreateInstance(
                    typeof(MemoryRepository<>).MakeGenericType(group.Key),
                    [typedItems])!;
            }

            return repositories;
        }

        public IReadOnlyList<TEntity> Items<TEntity>() where TEntity : BaseEntity =>
            Repository<TEntity>() is MemoryRepository<TEntity> repository ? repository.Items : [];

        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
        {
            if (!_repositories.TryGetValue(typeof(TEntity), out object? repository))
            {
                repository = new MemoryRepository<TEntity>([]);
                _repositories[typeof(TEntity)] = repository;
            }

            return (IGenericRepository<TEntity>)repository;
        }

        public Task ExecuteWithStrategyAsync(Func<Task> operation) => operation();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            Transaction = new MemoryTransaction();
            return Task.FromResult<IUnitOfWorkTransaction>(Transaction);
        }

        public Task<TResult> ExecuteWithStrategyAsync<TResult>(Func<Task<TResult>> operation) =>
            operation();
    }

    private sealed class MemoryRepository<TEntity>(IEnumerable<TEntity> seed) : IGenericRepository<TEntity>
        where TEntity : BaseEntity
    {
        private readonly List<TEntity> _items = seed.ToList();
        public IReadOnlyList<TEntity> Items => _items;

        public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.SingleOrDefault(item => item.Id == id));
        public Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.AsQueryable().FirstOrDefault(predicate));
        public Task<PagedResult<TEntity>> ListAsync(
            Expression<Func<TEntity, bool>>? predicate,
            PageRequest page,
            CancellationToken cancellationToken = default)
        {
            IQueryable<TEntity> query = _items.AsQueryable();
            if (predicate is not null) query = query.Where(predicate);
            TEntity[] items = query.Skip(page.Skip).Take(page.PageSize).ToArray();
            return Task.FromResult(new PagedResult<TEntity>(items, page.Page, page.PageSize, query.Count()));
        }
        public Task<IReadOnlyList<TResult>> ProjectAsync<TResult>(
            Expression<Func<TEntity, bool>>? predicate,
            Expression<Func<TEntity, TResult>> selector,
            CancellationToken cancellationToken = default)
        {
            IQueryable<TEntity> query = _items.AsQueryable();
            if (predicate is not null) query = query.Where(predicate);
            return Task.FromResult<IReadOnlyList<TResult>>(query.Select(selector).ToArray());
        }
        public Task<IReadOnlyList<TResult>> ProjectJoinAsync<TOther, TKey, TResult>(
            Expression<Func<TEntity, bool>>? predicate,
            Expression<Func<TOther, bool>>? otherPredicate,
            Expression<Func<TEntity, TKey>> keySelector,
            Expression<Func<TOther, TKey>> otherKeySelector,
            Expression<Func<TEntity, TOther, TResult>> selector,
            CancellationToken cancellationToken = default)
            where TOther : BaseEntity => throw new NotSupportedException();
        public Task<int> CountAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(predicate is null ? _items.Count : _items.AsQueryable().Count(predicate));
        public Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.AsQueryable().Any(predicate));
        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            _items.Add(entity);
            return Task.CompletedTask;
        }
        public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            _items.AddRange(entities);
            return Task.CompletedTask;
        }
        public void Update(TEntity entity) { }
        public void Remove(TEntity entity) => _items.Remove(entity);
        public void DeletePermanently(TEntity entity) => _items.Remove(entity);
    }

    private sealed class MemoryTransaction : IUnitOfWorkTransaction
    {
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RolledBack = true;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed record TestCurrentUser(
        Guid User,
        Guid Organization,
        OrganizationRole Role) : ICurrentUserContext
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => User;
        public Guid? PlatformUserId => null;
        public Guid? OrganizationId => Organization;
        public OrganizationRole? OrganizationRole => Role;
        public PlatformRole? PlatformRole => null;
        public string? IpAddress => null;
        public string? UserAgent => null;
    }

    private sealed record FixedClock(DateTimeOffset UtcNow) : IClock;

    private sealed class NoOpTenantScope : IAuthenticationTenantScope
    {
        public IDisposable Begin(Guid organizationId) => NoOpDisposable.Instance;
        public IDisposable BeginBypass() => NoOpDisposable.Instance;

        private sealed class NoOpDisposable : IDisposable
        {
            public static NoOpDisposable Instance { get; } = new();
            public void Dispose() { }
        }
    }

    private sealed class AllowSubscriptionAccess : ISubscriptionAccessService
    {
        public Task<SubscriptionAccess> GetAccessAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SubscriptionAccess(SubscriptionAccessMode.Full, null, null, null));
        public Task EnsureReadAllowedAsync(Guid organizationId, string? featureKey = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task EnsureWriteAllowedAsync(Guid organizationId, string? featureKey = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<Guid> Recipients { get; } = [];
        public Task CreateAsync(
            Guid organizationId,
            Guid userId,
            NotificationType type,
            string title,
            string body,
            IReadOnlyDictionary<string, string>? parameters = null,
            string? relatedEntityType = null,
            Guid? relatedEntityId = null,
            CancellationToken cancellationToken = default)
        {
            Recipients.Add(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingNotificationService(Guid failingUserId) : INotificationService
    {
        public Task CreateAsync(
            Guid organizationId,
            Guid userId,
            NotificationType type,
            string title,
            string body,
            IReadOnlyDictionary<string, string>? parameters = null,
            string? relatedEntityType = null,
            Guid? relatedEntityId = null,
            CancellationToken cancellationToken = default) =>
            userId == failingUserId
                ? Task.FromException(new InvalidOperationException("Notification persistence failed."))
                : Task.CompletedTask;
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task RecordTenantAsync(
            Guid organizationId,
            string action,
            string entityType,
            Guid? entityId,
            IReadOnlyDictionary<string, string>? oldValues = null,
            IReadOnlyDictionary<string, string>? newValues = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordPlatformAsync(
            string action,
            string entityType,
            Guid? entityId,
            Guid? organizationId = null,
            IReadOnlyDictionary<string, string>? oldValues = null,
            IReadOnlyDictionary<string, string>? newValues = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpFileStorage : IFileStorageService
    {
        public Task<StoredFile> SaveAsync(
            Stream content,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string url, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public string ResolveUrl(string? storagePathOrKey) => storagePathOrKey ?? string.Empty;
    }

    private sealed class NoOpDistributionCreator : ITaskDistributionCreator
    {
        public Task<TaskDistributionResponse> CreateAsync(
            TaskDistributionCreation creation,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
