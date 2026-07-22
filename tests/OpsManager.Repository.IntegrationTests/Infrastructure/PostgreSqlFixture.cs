using Microsoft.EntityFrameworkCore;
using OpsManager.Domain.Repositories;
using OpsManager.Repository.Persistence;
using Testcontainers.PostgreSql;

namespace OpsManager.Repository.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("opsmanager_tests")
        .WithUsername("postgres")
        .WithPassword("postgres_tests_only")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using OpsManagerDbContext context = CreateContext(new TestTenantContext(null, true));
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public OpsManagerDbContext CreateContext(ITenantContext tenantContext)
    {
        DbContextOptions<OpsManagerDbContext> options = new DbContextOptionsBuilder<OpsManagerDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new OpsManagerDbContext(options, tenantContext);
    }
}

public sealed record TestTenantContext(Guid? OrganizationId, bool BypassTenantFilter) : ITenantContext;
