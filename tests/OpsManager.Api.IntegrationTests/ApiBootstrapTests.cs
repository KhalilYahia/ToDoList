using System.Net;
using System.Net.Http.Json;
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
