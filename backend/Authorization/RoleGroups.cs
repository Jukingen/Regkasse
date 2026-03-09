namespace KasseAPI_Final.Authorization;

/// <summary>
/// Reusable role groups for legacy policy definitions.
/// Use in AuthorizationExtensions to reduce repeated RequireRole lists.
/// </summary>
public static class RoleGroups
{
    /// <summary>SuperAdmin, Admin. Use for admin-only policies (e.g. BackofficeSettings, SystemCritical).</summary>
    public static readonly string[] AdminOnly = { Roles.SuperAdmin, Roles.Admin };

    /// <summary>SuperAdmin, Admin, Manager. Use for backoffice management policies.</summary>
    public static readonly string[] BackofficeManagers = { Roles.SuperAdmin, Roles.Admin, Roles.Manager };

    /// <summary>Cashier, Manager, Admin, SuperAdmin. Aligned with PosSales / PosTse.</summary>
    public static readonly string[] PosSalesRoles = { Roles.Cashier, Roles.Manager, Roles.Admin, Roles.SuperAdmin };

    /// <summary>Waiter, Cashier, Manager, Admin, SuperAdmin. Aligned with PosTableOrder / PosCatalogRead.</summary>
    public static readonly string[] PosOrderRoles = { Roles.Waiter, Roles.Cashier, Roles.Manager, Roles.Admin, Roles.SuperAdmin };
}
