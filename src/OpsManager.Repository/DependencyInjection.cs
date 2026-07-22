using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpsManager.Domain.Repositories;
using OpsManager.Repository.Persistence;
using OpsManager.Repository.Seeding;

namespace OpsManager.Repository;

public static class DependencyInjection
{
    public static IServiceCollection AddOpsManagerRepository(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("OpsManager")
            ?? throw new InvalidOperationException("ConnectionStrings:OpsManager is required.");

        services.AddScoped<ITenantContext, EmptyTenantContext>();
        services.AddDbContext<OpsManagerDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(OpsManagerDbContext).Assembly.FullName)
                    .EnableRetryOnFailure()));
        services.AddScoped<IUnitOfWork, Repositories.UnitOfWork>();
        services.AddScoped<IDevelopmentDataSeeder, DevelopmentDataSeeder>();
        services.AddHealthChecks()
            .AddDbContextCheck<OpsManagerDbContext>("postgresql", tags: ["ready"]);
        return services;
    }

    private sealed class EmptyTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool BypassTenantFilter => false;
    }
}
