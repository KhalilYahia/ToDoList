using System.Security.Claims;
using OpsManager.Domain.Constants;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;

namespace OpsManager.Api.Security;

public sealed class RequestContext(IHttpContextAccessor httpContextAccessor) :
    ICurrentUserContext,
    ICurrentTenantContext,
    IAuthenticationTenantScope,
    ITenantContext
{
    private Guid? _scopedOrganizationId;
    private bool _bypassTenantFilter;

    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();

    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        User.HasClaim(claim => claim.Type == ClaimNames.OrganizationRole)
            ? ParseGuidClaim(ClaimTypes.NameIdentifier)
            : null;

    public Guid? PlatformUserId =>
        User.HasClaim(claim => claim.Type == ClaimNames.PlatformRole)
            ? ParseGuidClaim(ClaimTypes.NameIdentifier)
            : null;

    public Guid? OrganizationId => _scopedOrganizationId ?? ParseGuidClaim(ClaimNames.OrganizationId);

    public OrganizationRole? OrganizationRole =>
        ParseEnumClaim<OrganizationRole>(ClaimNames.OrganizationRole);

    public PlatformRole? PlatformRole =>
        ParseEnumClaim<PlatformRole>(ClaimNames.PlatformRole);

    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public bool BypassTenantFilter => _bypassTenantFilter || PlatformUserId.HasValue;

    public IDisposable Begin(Guid organizationId)
    {
        Guid? previous = _scopedOrganizationId;
        bool previousBypass = _bypassTenantFilter;
        _scopedOrganizationId = organizationId;
        _bypassTenantFilter = false;
        return new Scope(() =>
        {
            _scopedOrganizationId = previous;
            _bypassTenantFilter = previousBypass;
        });
    }

    public IDisposable BeginBypass()
    {
        bool previous = _bypassTenantFilter;
        _bypassTenantFilter = true;
        return new Scope(() => _bypassTenantFilter = previous);
    }

    private Guid? ParseGuidClaim(string claimType) =>
        Guid.TryParse(User.FindFirstValue(claimType), out Guid value) ? value : null;

    private TEnum? ParseEnumClaim<TEnum>(string claimType)
        where TEnum : struct, Enum =>
        Enum.TryParse(User.FindFirstValue(claimType), true, out TEnum value) ? value : null;

    private sealed class Scope(Action onDispose) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            onDispose();
            _disposed = true;
        }
    }
}
