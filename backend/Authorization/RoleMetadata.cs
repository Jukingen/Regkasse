namespace KasseAPI_Final.Authorization;

/// <summary>
/// Optional display metadata for roles. AspNetRoles stores only IdentityRole (Name); system/custom
/// flags and userCount are computed at runtime. This class supplies displayName/description for
/// API/UI without a DB migration. roleKey is stable identifier (canonical name or normalized custom name).
/// </summary>
public static class RoleMetadata
{
    /// <summary>
    /// Stable key for API/clients: canonical roles use constant name; custom roles use same as roleName (Identity stores unique Name).
    /// </summary>
    public static string GetRoleKey(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return string.Empty;
        return roleName.Trim();
    }

    /// <summary>
    /// System roles are immutable (matrix-only, not deletable). Same predicate as Roles.Canonical membership.
    /// </summary>
    public static bool IsImmutable(string roleName)
    {
        return Roles.Canonical.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Human-readable label for canonical roles; custom roles fall back to roleName.
    /// UI language is German elsewhere; API values English to match code identifiers policy.
    /// </summary>
    public static string GetDisplayName(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return string.Empty;
        if (DisplayNames.TryGetValue(roleName, out var display)) return display;
        return roleName;
    }

    /// <summary>
    /// Short description for canonical roles only.
    /// </summary>
    public static string? GetDescription(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return null;
        return Descriptions.TryGetValue(roleName, out var d) ? d : null;
    }

    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [Roles.SuperAdmin] = "SuperAdmin",
        [Roles.Manager] = "Manager",
        [Roles.Cashier] = "Cashier",
        [Roles.Waiter] = "Waiter",
        [Roles.Kitchen] = "Kitchen",
        [Roles.ReportViewer] = "ReportViewer",
        [Roles.Accountant] = "Accountant",
    };

    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        [Roles.SuperAdmin] = "Full system and user management; system roles cannot be mutated.",
        [Roles.Manager] = "Operational management, users view, most POS permissions.",
        [Roles.Cashier] = "Sales, payments, cart, TSE sign.",
        [Roles.Waiter] = "Tables, orders, limited payment.",
        [Roles.Kitchen] = "Kitchen display and order status.",
        [Roles.ReportViewer] = "Read-only reports and exports.",
        [Roles.Accountant] = "Audit and invoice view/export without write.",
    };
}
