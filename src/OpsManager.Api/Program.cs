using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpsManager.Api.Infrastructure;
using OpsManager.Api.Security;
using OpsManager.Domain.Constants;
using OpsManager.Domain.Repositories;
using OpsManager.Repository;
using OpsManager.Repository.Seeding;
using OpsManager.Service;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Auth;
using OpsManager.Service.Tasks;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "OpsManager API";
    document.Info.Version = "v1";
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Short-lived OpsManager organization or platform access token.",
    };
    document.Security ??= [];
    document.Security.Add(new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
    return Task.CompletedTask;
}));
JwtOptions jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (!builder.Environment.IsDevelopment() &&
    !builder.Environment.IsEnvironment("Testing") &&
    jwtOptions.SigningKey.StartsWith("development-only", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Jwt:SigningKey must be supplied from a production secret source.");
}

RefreshCookieOptions refreshCookieOptions =
    builder.Configuration.GetSection(RefreshCookieOptions.SectionName).Get<RefreshCookieOptions>() ?? new RefreshCookieOptions();
FileStorageOptions fileStorageOptions =
    builder.Configuration.GetSection(FileStorageOptions.SectionName).Get<FileStorageOptions>() ?? new FileStorageOptions();
OnboardingOptions onboardingOptions =
    builder.Configuration.GetSection("Onboarding").Get<OnboardingOptions>()
    ?? new OnboardingOptions("trial", 14, "Primary Branch");
SchedulerWorkerOptions schedulerWorkerOptions =
    builder.Configuration.GetSection(SchedulerWorkerOptions.SectionName).Get<SchedulerWorkerOptions>()
    ?? new SchedulerWorkerOptions();

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(refreshCookieOptions);
builder.Services.AddSingleton(fileStorageOptions);
builder.Services.AddSingleton(fileStorageOptions.Local);
builder.Services.AddSingleton<LocalFileStorageService>();
builder.Services.AddSingleton<IFileStorageService, DynamicFileStorageService>();
builder.Services.AddSingleton(onboardingOptions);
builder.Services.AddSingleton(schedulerWorkerOptions);
builder.Services.AddSingleton(new SchedulerOptions(schedulerWorkerOptions.GenerationHorizonDays));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<RequestContext>();
builder.Services.AddScoped<ICurrentUserContext>(services => services.GetRequiredService<RequestContext>());
builder.Services.AddScoped<ICurrentTenantContext>(services => services.GetRequiredService<RequestContext>());
builder.Services.AddScoped<IAuthenticationTenantScope>(services => services.GetRequiredService<RequestContext>());
builder.Services.AddScoped<ITenantContext>(services => services.GetRequiredService<RequestContext>());
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddHostedService<TaskOccurrenceGenerationWorker>();
builder.Services.AddHostedService<SubscriptionLifecycleWorker>();
builder.Services.AddHostedService<OperationalNotificationWorker>();
builder.Services.AddOpsManagerServices();
builder.Services.AddOpsManagerRepository(builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Authentication required",
                    detail: "A valid access token is required.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["traceId"] = context.HttpContext.TraceIdentifier,
                        ["code"] = "authentication_required",
                    }).ExecuteAsync(context.HttpContext);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    detail: "The authenticated identity does not satisfy this endpoint's policy.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["traceId"] = context.HttpContext.TraceIdentifier,
                        ["code"] = "forbidden",
                    }).ExecuteAsync(context.HttpContext);
            },
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(PolicyNames.OrganizationMember, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimNames.OrganizationId)
        .RequireClaim(ClaimNames.OrganizationRole))
    .AddPolicy(PolicyNames.Manager, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimNames.OrganizationRole, OrganizationRoles.Manager))
    .AddPolicy(PolicyNames.SupervisorOrManager, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimNames.OrganizationRole, OrganizationRoles.Manager, OrganizationRoles.Supervisor))
    .AddPolicy(PolicyNames.Employee, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(
            ClaimNames.OrganizationRole,
            OrganizationRoles.Manager,
            OrganizationRoles.Supervisor,
            OrganizationRoles.Employee))
    .AddPolicy(PolicyNames.PlatformUser, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimNames.PlatformRole))
    .AddPolicy(PolicyNames.PlatformAdministrator, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimNames.PlatformRole, PlatformRoles.Administrator));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("ConfiguredOrigins", policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
}));

// this for Caddy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

      options.KnownProxies.Clear();
});

WebApplication app = builder.Build();

// this for Caddy
app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseCors("ConfiguredOrigins");
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "OpsManager API v1");
        options.DocumentTitle = "OpsManager API";
        options.DisplayRequestDuration();
        options.EnableTryItOutByDefault();
    });
}

app.MapGet("/api/v1", () => Results.Ok(new
{
    name = "OpsManager API",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
}))
    .WithName("GetApiVersion");
app.MapControllers();

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
    app.MapGet(
        "/api/v1/test/concurrency",
        static () => Task.FromException<IResult>(new DbUpdateConcurrencyException("Intentional concurrency conflict.")));
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
