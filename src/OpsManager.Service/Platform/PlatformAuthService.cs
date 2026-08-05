using OpsManager.Domain.Entities;
using OpsManager.Domain.Enums;
using OpsManager.Domain.Repositories;
using OpsManager.Service.Abstractions;
using OpsManager.Service.Common;
using OpsManager.Service.Platform.DTOs;
using Task = System.Threading.Tasks.Task;

namespace OpsManager.Service.Platform;

public interface IPlatformAuthService
{
    Task<PlatformAuthenticationSession> LoginAsync(PlatformLoginRequest request, CancellationToken cancellationToken = default);
    Task<PlatformAuthenticationSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<PlatformUserDto> GetMeAsync(CancellationToken cancellationToken = default);
}

public sealed class PlatformAuthService(
    IUnitOfWork unitOfWork,
    IPasswordService passwordService,
    ITokenService tokenService,
    ICurrentUserContext currentUser,
    IClock clock,
    IAuditService auditService) : IPlatformAuthService
{
    public async Task<PlatformAuthenticationSession> LoginAsync(
        PlatformLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new AuthenticationFailedException();
        }

        string email = request.Email.Trim().ToUpperInvariant();
        PlatformUser? user = await unitOfWork.Repository<PlatformUser>()
            .FirstOrDefaultAsync(entity => entity.NormalizedEmail == email, cancellationToken);
        if (user is null ||
            user.Status != UserAccountStatus.Active ||
            !passwordService.VerifyPassword(user.PasswordHash, request.Password))
        {
            throw new AuthenticationFailedException();
        }

        user.LastLoginAt = clock.UtcNow;
        unitOfWork.Repository<PlatformUser>().Update(user);
        PlatformAuthenticationSession session = await CreateSessionAsync(user, null, null, cancellationToken);
        await auditService.RecordPlatformAsync(
            "platform-auth.login",
            nameof(PlatformUser),
            user.Id,
            cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<PlatformAuthenticationSession> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        string hash = tokenService.HashRefreshToken(refreshToken);
        RefreshToken token = await unitOfWork.Repository<RefreshToken>()
            .FirstOrDefaultAsync(entity => entity.TokenHash == hash && entity.PlatformUserId.HasValue, cancellationToken)
            ?? throw new AuthenticationFailedException();
        if (token.RevokedAt.HasValue)
        {
            await RevokeFamilyAsync(token.FamilyId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException("Refresh-token reuse was detected.");
        }

        if (token.ExpiresAt <= clock.UtcNow)
        {
            throw new AuthenticationFailedException("The refresh token has expired.");
        }

        PlatformUser user = await unitOfWork.Repository<PlatformUser>()
            .GetByIdAsync(token.PlatformUserId!.Value, cancellationToken)
            ?? throw new AuthenticationFailedException();
        if (user.Status != UserAccountStatus.Active)
        {
            throw new AuthenticationFailedException();
        }

        Guid replacementId = Guid.NewGuid();
        PlatformAuthenticationSession session =
            await CreateSessionAsync(user, token.FamilyId, replacementId, cancellationToken);
        token.Revoke(clock.UtcNow, currentUser.IpAddress, replacementId, "rotated");
        unitOfWork.Repository<RefreshToken>().Update(token);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        string hash = tokenService.HashRefreshToken(refreshToken);
        RefreshToken? token = await unitOfWork.Repository<RefreshToken>()
            .FirstOrDefaultAsync(entity => entity.TokenHash == hash && entity.PlatformUserId.HasValue, cancellationToken);
        if (token is not null && token.RevokedAt is null)
        {
            token.Revoke(clock.UtcNow, currentUser.IpAddress, null, "logout");
            unitOfWork.Repository<RefreshToken>().Update(token);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<PlatformUserDto> GetMeAsync(CancellationToken cancellationToken = default)
    {
        Guid id = currentUser.PlatformUserId
            ?? throw new AuthenticationFailedException();
        PlatformUser user = await unitOfWork.Repository<PlatformUser>().GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(PlatformUser));
        return Map(user);
    }

    private async Task<PlatformAuthenticationSession> CreateSessionAsync(
        PlatformUser user,
        Guid? familyId,
        Guid? tokenId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.UtcNow;
        IssuedAccessToken access = tokenService.CreateAccessToken(
            new AccessTokenDescriptor(user.Id, user.Email, now, PlatformRole: user.Role));
        string rawToken = tokenService.GenerateRefreshToken();
        DateTimeOffset expires = now.Add(tokenService.RefreshTokenLifetime);
        RefreshToken token = new(
            null,
            user.Id,
            null,
            familyId ?? Guid.NewGuid(),
            tokenService.HashRefreshToken(rawToken),
            expires)
        {
            Id = tokenId ?? Guid.NewGuid(),
            CreatedByIp = currentUser.IpAddress,
        };
        await unitOfWork.Repository<RefreshToken>().AddAsync(token, cancellationToken);
        return new PlatformAuthenticationSession(
            new PlatformAuthenticationResponse(access.Token, access.ExpiresAt, Map(user)),
            rawToken,
            expires);
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        PagedResult<RefreshToken> family = await unitOfWork.Repository<RefreshToken>().ListAsync(
            token => token.FamilyId == familyId,
            new PageRequest(1, PageRequest.MaximumPageSize),
            cancellationToken);
        foreach (RefreshToken token in family.Items.Where(entity => entity.RevokedAt is null))
        {
            token.Revoke(clock.UtcNow, currentUser.IpAddress, null, "refresh-token reuse detected");
            unitOfWork.Repository<RefreshToken>().Update(token);
        }
    }

    private static PlatformUserDto Map(PlatformUser user) =>
        new(user.Id, user.FullName, user.Email, user.Role, user.PreferredLanguage);
}
