namespace KasseAPI_Final.Authorization;

/// <summary>
/// Reusable role groups for legacy policy definitions.
/// Use in AuthorizationExtensions to reduce repeated RequireRole lists.
/// Admin role removed; SuperAdmin is sole top-level admin.
/// </summary>
public static class RoleGroups
{
    /// <summary>SuperAdmin only. Use for admin-only policies (e.g. BackofficeSettings, SystemCritical).</summary>
    public static readonly string[] AdminOnly = { Roles.SuperAdmin };

    /// <summary>SuperAdmin, Manager. Use for backoffice management policies.</summary>
    public static readonly string[] BackofficeManagers = { Roles.SuperAdmin, Roles.Manager };

    /// <summary>Cashier, Manager, SuperAdmin. Aligned with PosSales / PosTse.</summary>
    public static readonly string[] PosSalesRoles = { Roles.Cashier, Roles.Manager, Roles.SuperAdmin };

    /// <summary>Waiter, Cashier, Manager, SuperAdmin. Aligned with PosTableOrder / PosCatalogRead.</summary>
    public static readonly string[] PosOrderRoles = { Roles.Waiter, Roles.Cashier, Roles.Manager, Roles.SuperAdmin };
}
