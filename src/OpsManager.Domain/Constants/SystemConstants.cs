namespace OpsManager.Domain.Constants;

public static class ClaimNames
{
    public const string OrganizationId = "organization_id";
    public const string OrganizationRole = "organization_role";
    public const string PlatformRole = "platform_role";
}

public static class OrganizationRoles
{
    public const string Manager = "Manager";
    public const string Supervisor = "Supervisor";
    public const string Employee = "Employee";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Manager,
        Supervisor,
        Employee,
    };
}

public static class PlatformRoles
{
    public const string Administrator = "Administrator";
    public const string Support = "Support";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Administrator,
        Support,
    };
}

public static class SupportedLanguages
{
    public const string Arabic = "ar";
    public const string English = "en";
    public const string Russian = "ru";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Arabic,
        English,
        Russian,
    };
}

public static class SubscriptionFeatureKeys
{
    public const string Tasks = "tasks";
    public const string DepartmentOrders = "department_orders";
    public const string Complaints = "complaints";
    public const string Reports = "reports";
}

public static class PolicyNames
{
    public const string OrganizationMember = "OrganizationMember";
    public const string Manager = "Manager";
    public const string SupervisorOrManager = "SupervisorOrManager";
    public const string Employee = "Employee";
    public const string PlatformUser = "PlatformUser";
    public const string PlatformAdministrator = "PlatformAdministrator";
}
