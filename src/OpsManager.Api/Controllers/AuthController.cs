using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpsManager.Api.Infrastructure;
using OpsManager.Domain.Constants;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Auth;
using OpsManager.Service.Auth.DTOs;
using OpsManager.Service.Common;

namespace OpsManager.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(
    IAuthService authService,
    RefreshCookieOptions cookieOptions) : ControllerBase
{
    [HttpPost("register-organization")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthenticationResponse>> RegisterOrganization(
        RegisterOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        AuthenticationSession session = await authService.RegisterOrganizationAsync(request, cancellationToken);
        RefreshCookie.Set(Response, cookieOptions, session.RefreshToken, session.RefreshTokenExpiresAt);
        return StatusCode(StatusCodes.Status201Created, session.Response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthenticationResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        AuthenticationSession session = await authService.LoginAsync(request, cancellationToken);
        RefreshCookie.Set(Response, cookieOptions, session.RefreshToken, session.RefreshTokenExpiresAt);
        return Ok(session.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthenticationResponse>> Refresh(CancellationToken cancellationToken)
    {
        string token = RequireRefreshCookie();
        AuthenticationSession session = await authService.RefreshAsync(token, cancellationToken);
        RefreshCookie.Set(Response, cookieOptions, session.RefreshToken, session.RefreshTokenExpiresAt);
        return Ok(session.Response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(cookieOptions.Name, out string? token) && !string.IsNullOrWhiteSpace(token))
        {
            await authService.LogoutAsync(token, cancellationToken);
        }

        RefreshCookie.Delete(Response, cookieOptions);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize(Policy = PolicyNames.OrganizationMember)]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    public Task<MeResponse> Me(CancellationToken cancellationToken) =>
        authService.GetMeAsync(cancellationToken);

    [HttpPost("change-password")]
    [Authorize(Policy = PolicyNames.OrganizationMember)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await authService.ChangePasswordAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("profile")]
    [Authorize(Policy = PolicyNames.OrganizationMember)]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    public Task<CurrentUserDto> UpdateProfile(
        UpdateProfileRequest request,
        CancellationToken cancellationToken) =>
        authService.UpdateProfileAsync(request, cancellationToken);

    [HttpPost("avatar")]
    [Authorize(Policy = PolicyNames.OrganizationMember)]
    [ProducesResponseType<StoredFile>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StoredFile>> UploadAvatar(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty avatar file is required.");
        }

        await using Stream stream = file.OpenReadStream();
        StoredFile result = await authService.UploadMyAvatarAsync(
            stream,
            file.FileName,
            file.ContentType,
            cancellationToken);
        return Ok(result);
    }

    private string RequireRefreshCookie()
    {
        if (!Request.Cookies.TryGetValue(cookieOptions.Name, out string? token) || string.IsNullOrWhiteSpace(token))
        {
            throw new AuthenticationFailedException("The refresh cookie is missing.");
        }

        return token;
    }
}
