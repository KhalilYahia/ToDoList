using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpsManager.Api.IntegrationTests.Infrastructure;
using Testcontainers.PostgreSql;

namespace OpsManager.Api.IntegrationTests;

public sealed class ApiBootstrapTests
{
    [Fact]
    public async Task Api_starts_and_version_endpoint_succeeds()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Liveness_succeeds_without_a_database()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(Timeout = 30_000)]
    public async Task Readiness_is_unavailable_when_postgresql_is_unavailable()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Unhandled_exception_returns_problem_details()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/test/error");
        request.Headers.Accept.ParseAdd("application/problem+json");

        HttpResponseMessage response = await client.SendAsync(request);
        ProblemDetailsResponse? problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(500, problem.Status);
        Assert.DoesNotContain("Intentional integration-test failure", problem.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concurrency_exception_returns_safe_conflict_problem_details()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/test/concurrency");
        ProblemDetailsResponse? problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(409, problem?.Status);
        Assert.Contains("modified by another request", problem?.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Intentional concurrency conflict", problem?.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_document_is_generated()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");
        string document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/v1/tasks", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/platform/subscription-plans", document, StringComparison.Ordinal);
        Assert.Contains(
            "/api/v1/platform/organizations/{organizationId}/branches",
            document,
            StringComparison.Ordinal);
        Assert.Contains("securitySchemes", document, StringComparison.Ordinal);
        using JsonDocument openApi = JsonDocument.Parse(document);
        string recurrenceType = openApi.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("RecurrenceType")
            .GetProperty("type")
            .GetString()!;
        Assert.Equal("integer", recurrenceType);
        Assert.DoesNotContain("recurrenceRule", document, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TaskDistributionResponse", document, StringComparison.Ordinal);
        Assert.Contains("TaskAssignmentRequest", document, StringComparison.Ordinal);
        Assert.Contains("taskDistributionId", document, StringComparison.Ordinal);
        Assert.DoesNotContain("defaultAssigneeUserId", document, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Swagger_ui_renders_the_generated_OpenApi_document()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage pageResponse = await client.GetAsync("/swagger/index.html");
        HttpResponseMessage initializerResponse = await client.GetAsync("/swagger/index.js");
        string page = await pageResponse.Content.ReadAsStringAsync();
        string initializer = await initializerResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, initializerResponse.StatusCode);
        Assert.Contains("OpsManager API", page, StringComparison.Ordinal);
        Assert.Contains("/openapi/v1.json", initializer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cors_preflight_allows_the_configured_loopback_frontend()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "http://127.0.0.1:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "http://127.0.0.1:3000",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal(
            "true",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.Contains(
            "POST",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Methods")),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cors_preflight_does_not_allow_an_unconfigured_origin()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "http://example.invalid");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [DockerFact]
    public async Task Readiness_succeeds_when_postgresql_is_available()
    {
        await using PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("opsmanager_api_tests")
            .WithUsername("postgres")
            .WithPassword("postgres_tests_only")
            .Build();
        await container.StartAsync();
        await using OpsManagerApiFactory factory = new(container.GetConnectionString());
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record ProblemDetailsResponse(int? Status, string? Title, string? Detail);
}
