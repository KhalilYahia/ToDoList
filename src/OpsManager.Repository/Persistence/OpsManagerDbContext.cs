using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OpsManager.Domain.Common;
using OpsManager.Domain.Entities;
using OpsManager.Domain.Repositories;

namespace OpsManager.Repository.Persistence;

public sealed class OpsManagerDbContext(
    DbContextOptions<OpsManagerDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public Guid? CurrentOrganizationId => tenantContext.OrganizationId;

    public bool BypassTenantFilter => tenantContext.BypassTenantFilter;

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<UserDepartment> UserDepartments => Set<UserDepartment>();
    public DbSet<TaskTemplate> TaskTemplates => Set<TaskTemplate>();
    public DbSet<TaskTemplateItem> TaskTemplateItems => Set<TaskTemplateItem>();
    public DbSet<TaskTemplateItemAttachment> TaskTemplateItemAttachments => Set<TaskTemplateItemAttachment>();
    public DbSet<TaskDistribution> TaskDistributions => Set<TaskDistribution>();
    public DbSet<TaskSchedule> TaskSchedules => Set<TaskSchedule>();
    public DbSet<TaskScheduleAssignee> TaskScheduleAssignees => Set<TaskScheduleAssignee>();
    public DbSet<TaskScheduleDate> TaskScheduleDates => Set<TaskScheduleDate>();
    public DbSet<OperationalTask> Tasks => Set<OperationalTask>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
    public DbSet<TaskStatusHistory> TaskStatusHistories => Set<TaskStatusHistory>();
    public DbSet<TaskAssignmentHistory> TaskAssignmentHistories => Set<TaskAssignmentHistory>();
    public DbSet<OrderTemplate> OrderTemplates => Set<OrderTemplate>();
    public DbSet<OrderTemplateItem> OrderTemplateItems => Set<OrderTemplateItem>();
    public DbSet<DepartmentOrder> DepartmentOrders => Set<DepartmentOrder>();
    public DbSet<DepartmentOrderItem> DepartmentOrderItems => Set<DepartmentOrderItem>();
    public DbSet<DepartmentOrderAttachment> DepartmentOrderAttachments => Set<DepartmentOrderAttachment>();
    public DbSet<DepartmentOrderStatusHistory> DepartmentOrderStatusHistories => Set<DepartmentOrderStatusHistory>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<ComplaintMessage> ComplaintMessages => Set<ComplaintMessage>();
    public DbSet<ComplaintAttachment> ComplaintAttachments => Set<ComplaintAttachment>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<OrganizationSubscription> OrganizationSubscriptions => Set<OrganizationSubscription>();
    public DbSet<SubscriptionHistory> SubscriptionHistories => Set<SubscriptionHistory>();
    public DbSet<ManualPayment> ManualPayments => Set<ManualPayment>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PlatformAuditLog> PlatformAuditLogs => Set<PlatformAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpsManagerDbContext).Assembly);
        ApplyGlobalFilters(modelBuilder);
        ApplyRelationalConventions(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<IAuditableEntity> entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = entry.Entity.CreatedAt == default ? now : entry.Entity.CreatedAt;
                entry.Entity.UpdatedAt = entry.Entity.UpdatedAt == default ? entry.Entity.CreatedAt : entry.Entity.UpdatedAt;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            Type clrType = entityType.ClrType;
            ParameterExpression parameter = Expression.Parameter(clrType, "entity");
            Expression? body = null;

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                Expression deletedAt = Expression.Property(parameter, nameof(ISoftDeletable.DeletedAt));
                body = Expression.Equal(deletedAt, Expression.Constant(null, typeof(DateTimeOffset?)));
            }

            if (typeof(ITenantEntity).IsAssignableFrom(clrType))
            {
                Expression organizationId = Expression.Convert(
                    Expression.Property(parameter, nameof(ITenantEntity.OrganizationId)),
                    typeof(Guid?));
                Expression tenantMatches = Expression.AndAlso(
                    Expression.Property(Expression.Property(Expression.Constant(this), nameof(CurrentOrganizationId)), nameof(Nullable<Guid>.HasValue)),
                    Expression.Equal(organizationId, Expression.Property(Expression.Constant(this), nameof(CurrentOrganizationId))));
                Expression tenantBody = Expression.OrElse(
                    Expression.Property(Expression.Constant(this), nameof(BypassTenantFilter)),
                    tenantMatches);
                body = body is null ? tenantBody : Expression.AndAlso(body, tenantBody);
            }
            else if (clrType == typeof(Organization))
            {
                Expression organizationId = Expression.Convert(
                    Expression.Property(parameter, nameof(BaseEntity.Id)),
                    typeof(Guid?));
                Expression tenantMatches = Expression.AndAlso(
                    Expression.Property(Expression.Property(Expression.Constant(this), nameof(CurrentOrganizationId)), nameof(Nullable<Guid>.HasValue)),
                    Expression.Equal(organizationId, Expression.Property(Expression.Constant(this), nameof(CurrentOrganizationId))));
                Expression tenantBody = Expression.OrElse(
                    Expression.Property(Expression.Constant(this), nameof(BypassTenantFilter)),
                    tenantMatches);
                body = body is null ? tenantBody : Expression.AndAlso(body, tenantBody);
            }

            if (body is not null)
            {
                entityType.SetQueryFilter(Expression.Lambda(body, parameter));
            }
        }
    }

    private static void ApplyRelationalConventions(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            string? tableName = entityType.GetTableName();
            if (tableName is not null)
            {
                entityType.SetTableName(ToSnakeCase(tableName));
            }

            foreach (IMutableProperty property in entityType.GetProperties())
            {
                property.SetColumnName(
                    entityType.ClrType == typeof(TaskSchedule) && property.Name == "_weekdays"
                        ? "weekdays"
                        : property.ClrType == typeof(uint) &&
                    property.IsConcurrencyToken &&
                    property.ValueGenerated == ValueGenerated.OnAddOrUpdate
                        ? "xmin"
                        : ToSnakeCase(property.Name));
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetColumnType("timestamptz");
                }
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        System.Text.StringBuilder builder = new(value.Length + 8);
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsUpper(current) && index > 0 && value[index - 1] != '_')
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
