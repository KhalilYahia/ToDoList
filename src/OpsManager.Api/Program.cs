using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpsManager.Repository;
using OpsManager.Repository.Seeding;
using OpsManager.Service.Abstractions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "OpsManager API";
    document.Info.Version = "v1";
    return Task.CompletedTask;
}));
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddOpsManagerRepository(builder.Configuration);

string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("ConfiguredOrigins", policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
}));

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseCors("ConfiguredOrigins");

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
}

app.MapGet("/api/v1", () => Results.Ok(new
{
    name = "OpsManager API",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
}))
    .WithName("GetApiVersion");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet(
        "/api/v1/test/error",
        static () => Task.FromException<IResult>(new InvalidOperationException("Intentional integration-test failure.")));
}

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Seed:Enabled"))
{
    string? seedPassword = builder.Configuration["Seed:Password"];
    if (string.IsNullOrWhiteSpace(seedPassword) || seedPassword.Length < 12)
    {
        throw new InvalidOperationException("Seed:Password must contain at least 12 characters when development seeding is enabled.");
    }

    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    IDevelopmentDataSeeder seeder = scope.ServiceProvider.GetRequiredService<IDevelopmentDataSeeder>();
    PasswordHasher<object> passwordHasher = new();
    string passwordHash = passwordHasher.HashPassword(new object(), seedPassword);
    await seeder.SeedAsync(passwordHash);
}

await app.RunAsync();

public partial class Program;
