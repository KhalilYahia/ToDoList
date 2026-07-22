using Microsoft.EntityFrameworkCore;
using OpsManager.Domain.Common;
using OpsManager.Repository.IntegrationTests.Infrastructure;
using OpsManager.Repository.Persistence;

namespace OpsManager.Repository.IntegrationTests;

public sealed class RepositoryModelTests
{
    [Fact]
    public void Tenant_query_filter_compiles_for_postgresql()
    {
        Guid organizationId = Guid.NewGuid();
        using OpsManagerDbContext context = CreateContext(new TestTenantContext(organizationId, false));

        string sql = context.Branches.AsNoTracking().ToQueryString();

        Assert.Contains("organization_id", sql, StringComparison.Ordinal);
        Assert.Contains("deleted_at", sql, StringComparison.Ordinal);
        Assert.Contains("@ef_filter__", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_tenant_entity_has_a_global_query_filter()
    {
        using OpsManagerDbContext context = CreateContext(new TestTenantContext(Guid.NewGuid(), false));
        Type[] unfilteredTenantTypes = context.Model.GetEntityTypes()
            .Where(entityType => typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            .Where(entityType => entityType.GetDeclaredQueryFilters().Count == 0)
            .Select(entityType => entityType.ClrType)
            .ToArray();

        Assert.Empty(unfilteredTenantTypes);
    }

    private static OpsManagerDbContext CreateContext(TestTenantContext tenantContext)
    {
        DbContextOptions<OpsManagerDbContext> options = new DbContextOptionsBuilder<OpsManagerDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=model_only;Username=none;Password=none")
            .Options;
        return new OpsManagerDbContext(options, tenantContext);
    }
}
