namespace SagaSales.Identity.Configuration;

/// <summary>
/// Role names used by the platform. The previous version declared nested <c>Role</c>/<c>User</c>
/// types alongside properties of the same name, which does not compile.
/// </summary>
public static class IdentityRoles
{
    public const string Admin = "admin";
    public const string Manager = "manager";
    public const string Sales = "sales";
    public const string Customer = "customer";

    public static readonly string[] All = [Admin, Manager, Sales, Customer];
}

public static class IdentityPolicies
{
    public const string RequiresAdmin = "RequireAdmin";
    public const string RequiresManager = "RequireManager";
    public const string RequiresSales = "RequireSales";
    public const string RequiresCustomer = "RequireCustomer";
    public const string RequiresTenantAccess = "RequireTenantAccess";
}
