using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpsManager.Api.Infrastructure;
using OpsManager.Domain.Constants;
using OpsManager.Service.Common;
using OpsManager.Service.Platform;
using OpsManager.Service.Platform.DTOs;

namespace OpsManager.Api.Controllers.Platform;

[ApiController]
[Route("api/v1/platform/auth")]
[EnableRateLimiting("auth")]
public sealed class PlatformAuthController(
    IPlatformAuthService service,
    RefreshCookieOptions cookieOptions) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<PlatformAuthenticationResponse> Login(
        PlatformLoginRequest request,
        CancellationToken cancellationToken)
    {
        PlatformAuthenticationSession session = await service.LoginAsync(request, cancellationToken);
        RefreshCookie.Set(Response, cookieOptions, session.RefreshToken, session.RefreshTokenExpiresAt);
        return session.Response;
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<PlatformAuthenticationResponse> Refresh(CancellationToken cancellationToken)
    {
        PlatformAuthenticationSession session =
            await service.RefreshAsync(RequireCookie(), cancellationToken);
        RefreshCookie.Set(Response, cookieOptions, session.RefreshToken, session.RefreshTokenExpiresAt);
        return session.Response;
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(cookieOptions.Name, out string? token) && !string.IsNullOrWhiteSpace(token))
        {
            await service.LogoutAsync(token, cancellationToken);
        }

        RefreshCookie.Delete(Response, cookieOptions);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize(Policy = PolicyNames.PlatformUser)]
    public Task<PlatformUserDto> Me(CancellationToken cancellationToken) =>
        service.GetMeAsync(cancellationToken);

    private string RequireCookie()
    {
        if (!Request.Cookies.TryGetValue(cookieOptions.Name, out string? token) || string.IsNullOrWhiteSpace(token))
        {
            throw new AuthenticationFailedException("The refresh cookie is missing.");
        }

        return token;
    }
}
