using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpsManager.Domain.Repositories;

namespace OpsManager.Repository.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OpsManagerDbContext>
{
    public OpsManagerDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("OPSMANAGER_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=opsmanager;Username=opsmanager;Password=opsmanager_dev";
        DbContextOptions<OpsManagerDbContext> options = new DbContextOptionsBuilder<OpsManagerDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(OpsManagerDbContext).Assembly.FullName))
            .Options;
        return new OpsManagerDbContext(options, DesignTimeTenantContext.Instance);
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public static DesignTimeTenantContext Instance { get; } = new();
        public Guid? OrganizationId => null;
        public bool BypassTenantFilter => true;
    }
}
