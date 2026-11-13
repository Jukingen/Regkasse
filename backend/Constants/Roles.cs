namespace KasseAPI_Final.Constants;

/// <summary>
/// Canonical role name constants. Values match <see cref="Authorization.Roles"/> and
/// <see cref="Authorization.RoleHierarchy.PlatformAdmin"/> so Identity, JWT, and policies stay stable.
/// </summary>
public static class Roles
{
    public const string SuperAdmin = Authorization.Roles.SuperAdmin;
    public const string PlatformAdmin = Authorization.RoleHierarchy.PlatformAdmin;
    public const string Manager = Authorization.Roles.Manager;
    public const string Cashier = Authorization.Roles.Cashier;
    public const string Kitchen = Authorization.Roles.Kitchen;
    public const string Waiter = Authorization.Roles.Waiter;
    public const string Accountant = Authorization.Roles.Accountant;
    public const string ReportViewer = Authorization.Roles.ReportViewer;
    public const string ComplianceOfficer = Authorization.Roles.ComplianceOfficer;
}
