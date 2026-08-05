using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using OpsManager.Api.IntegrationTests.Infrastructure;
using OpsManager.Api.Security;
using OpsManager.Domain.Constants;

namespace OpsManager.Api.IntegrationTests;

public sealed class AuthenticationBoundaryTests
{
    private const string SigningKey = "development-only-signing-key-change-before-production-2026";

    [Fact]
    public async Task Protected_endpoint_returns_problem_details_without_token()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/organization");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("authentication_required", body, StringComparison.Ordinal);
        Assert.Contains("traceId", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_and_expired_tokens_are_rejected()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");
        HttpResponseMessage invalid = await client.GetAsync("/api/v1/organization");

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken("Employee", DateTime.UtcNow.AddMinutes(-5)));
        HttpResponseMessage expired = await client.GetAsync("/api/v1/organization");

        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [Fact]
    public async Task Employee_token_cannot_use_manager_policy()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken("Employee", DateTime.UtcNow.AddMinutes(5)));

        using StringContent content = new("{}", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.PostAsync("/api/v1/departments", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_cannot_mutate_tenant_or_platform_branch_routes()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                CreateToken("Manager", DateTime.UtcNow.AddMinutes(5)));

        using StringContent tenantContent = new("{}", Encoding.UTF8, "application/json");
        HttpResponseMessage tenantResponse =
            await client.PostAsync("/api/v1/branches", tenantContent);

        using StringContent platformContent = new("{}", Encoding.UTF8, "application/json");
        HttpResponseMessage platformResponse = await client.PostAsync(
            $"/api/v1/platform/organizations/{Guid.NewGuid()}/branches",
            platformContent);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, tenantResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, platformResponse.StatusCode);
    }

    [Fact]
    public async Task Platform_support_cannot_mutate_organization_branches()
    {
        await using OpsManagerApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                CreatePlatformToken("Support", DateTime.UtcNow.AddMinutes(5)));

        using StringContent content = new("{}", Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/platform/organizations/{Guid.NewGuid()}/branches",
            content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Verified_claim_tenant_is_not_overridden_by_untrusted_values()
    {
        Guid claimTenant = Guid.NewGuid();
        DefaultHttpContext httpContext = new();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimNames.OrganizationId, claimTenant.ToString()),
            new Claim(ClaimNames.OrganizationRole, "Manager"),
        ], "test"));
        HttpContextAccessor accessor = new() { HttpContext = httpContext };
        RequestContext context = new(accessor);

        Assert.Equal(claimTenant, context.OrganizationId);
        using (context.Begin(Guid.NewGuid()))
        {
            Assert.NotEqual(claimTenant, context.OrganizationId);
        }

        Assert.Equal(claimTenant, context.OrganizationId);
    }

    private static string CreateToken(string role, DateTime expires)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(SigningKey));
        JwtSecurityToken token = new(
            issuer: "OpsManager",
            audience: "OpsManager.Client",
            claims:
            [
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimNames.OrganizationId, Guid.NewGuid().ToString()),
                new Claim(ClaimNames.OrganizationRole, role),
            ],
            notBefore: expires.AddMinutes(-10),
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreatePlatformToken(string role, DateTime expires)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(SigningKey));
        JwtSecurityToken token = new(
            issuer: "OpsManager",
            audience: "OpsManager.Client",
            claims:
            [
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimNames.PlatformRole, role),
            ],
            notBefore: expires.AddMinutes(-10),
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
