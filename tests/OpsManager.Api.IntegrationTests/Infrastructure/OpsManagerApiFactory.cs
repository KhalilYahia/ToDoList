using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OpsManager.Api.IntegrationTests.Infrastructure;

public sealed class OpsManagerApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public OpsManagerApiFactory()
        : this("Host=127.0.0.1;Port=1;Database=unavailable;Username=none;Password=none;Timeout=1;Command Timeout=1")
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:OpsManager", connectionString);
        builder.UseSetting("Database:EnableRetryOnFailure", "false");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OpsManager"] = connectionString,
                ["Seed:Enabled"] = "false",
            });
        });
    }
}
