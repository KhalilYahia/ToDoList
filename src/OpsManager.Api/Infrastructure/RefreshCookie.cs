namespace OpsManager.Api.Infrastructure;

public sealed class RefreshCookieOptions
{
    public const string SectionName = "RefreshCookie";
    public string Name { get; set; } = "opsmanager_refresh";
    public bool Secure { get; set; } = true;
    public string SameSite { get; set; } = "Strict";
}

public static class RefreshCookie
{
    public static void Set(HttpResponse response, RefreshCookieOptions options, string token, DateTimeOffset expiresAt)
    {
        response.Cookies.Append(options.Name, token, Build(options, expiresAt));
    }

    public static void Delete(HttpResponse response, RefreshCookieOptions options)
    {
        response.Cookies.Delete(options.Name, Build(options, DateTimeOffset.UnixEpoch));
    }

    private static CookieOptions Build(RefreshCookieOptions options, DateTimeOffset expiresAt) =>
        new()
        {
            HttpOnly = true,
            Secure = options.Secure,
            SameSite = Enum.TryParse(options.SameSite, true, out SameSiteMode sameSite)
                ? sameSite
                : SameSiteMode.Strict,
            Path = "/api/v1",
            Expires = expiresAt,
            IsEssential = true,
        };
}
