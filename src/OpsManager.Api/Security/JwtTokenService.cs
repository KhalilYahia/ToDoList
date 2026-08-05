using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OpsManager.Domain.Constants;
using OpsManager.Service.Abstractions;

namespace OpsManager.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "OpsManager";
    public string Audience { get; set; } = "OpsManager.Client";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

public sealed class JwtTokenService(JwtOptions options) : ITokenService
{
    private readonly byte[] _signingKey = ValidateAndEncode(options.SigningKey);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(options.RefreshTokenDays);

    public IssuedAccessToken CreateAccessToken(AccessTokenDescriptor descriptor)
    {
        DateTimeOffset expiresAt = descriptor.IssuedAt.AddMinutes(options.AccessTokenMinutes);
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, descriptor.SubjectId.ToString()),
            new(ClaimTypes.NameIdentifier, descriptor.SubjectId.ToString()),
            new(JwtRegisteredClaimNames.Email, descriptor.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, descriptor.IssuedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        ];

        if (descriptor.OrganizationId.HasValue && descriptor.OrganizationRole.HasValue)
        {
            claims.Add(new Claim(ClaimNames.OrganizationId, descriptor.OrganizationId.Value.ToString()));
            claims.Add(new Claim(ClaimNames.OrganizationRole, descriptor.OrganizationRole.Value.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, descriptor.OrganizationRole.Value.ToString()));
        }

        if (descriptor.PlatformRole.HasValue)
        {
            claims.Add(new Claim(ClaimNames.PlatformRole, descriptor.PlatformRole.Value.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, descriptor.PlatformRole.Value.ToString()));
        }

        SigningCredentials credentials = new(
            new SymmetricSecurityKey(_signingKey),
            SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(
            options.Issuer,
            options.Audience,
            claims,
            descriptor.IssuedAt.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);
        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static byte[] ValidateAndEncode(string signingKey)
    {
        byte[] value = Encoding.UTF8.GetBytes(signingKey);
        if (value.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
        }

        return value;
    }
}
