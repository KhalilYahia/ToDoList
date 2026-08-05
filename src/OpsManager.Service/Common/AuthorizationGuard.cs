using OpsManager.Domain.Enums;
using OpsManager.Service.Abstractions;

namespace OpsManager.Service.Common;

internal static class AuthorizationGuard
{
    public static Guid RequireOrganization(ICurrentUserContext currentUser)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null || currentUser.OrganizationId is null)
        {
            throw new AuthenticationFailedException();
        }

        return currentUser.OrganizationId.Value;
    }

    public static Guid RequireUser(ICurrentUserContext currentUser)
    {
        _ = RequireOrganization(currentUser);
        return currentUser.UserId!.Value;
    }

    public static Guid RequireManager(ICurrentUserContext currentUser)
    {
        Guid organizationId = RequireOrganization(currentUser);
        if (currentUser.OrganizationRole != OrganizationRole.Manager)
        {
            throw new ForbiddenAccessException("Manager access is required.");
        }

        return organizationId;
    }

    public static Guid RequireSupervisorOrManager(ICurrentUserContext currentUser)
    {
        Guid organizationId = RequireOrganization(currentUser);
        if (currentUser.OrganizationRole is not (OrganizationRole.Manager or OrganizationRole.Supervisor))
        {
            throw new ForbiddenAccessException("Supervisor or Manager access is required.");
        }

        return organizationId;
    }

    public static Guid RequirePlatformAdministrator(ICurrentUserContext currentUser)
    {
        if (!currentUser.IsAuthenticated || currentUser.PlatformUserId is null ||
            currentUser.PlatformRole != PlatformRole.Administrator)
        {
            throw new ForbiddenAccessException("Platform administrator access is required.");
        }

        return currentUser.PlatformUserId.Value;
    }
}
