namespace KasseAPI_Final.Authorization;

/// <summary>
/// Canonical role names for POS authorization. Use these constants instead of magic strings.
/// Identity (AspNetRoles) and JWT use the same names.
/// Single top-level admin: SuperAdmin. Admin role removed after migration MigrateAdminToSuperAdminAndDropAdminRole.
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";

    public const string Manager = "Manager";
    public const string Cashier = "Cashier";
    public const string Waiter = "Waiter";

    /// <summary>
    /// Optional: kitchen display / order status updates.
    /// </summary>
    public const string Kitchen = "Kitchen";

    /// <summary>
    /// Optional: read-only reports and exports.
    /// </summary>
    public const string ReportViewer = "ReportViewer";

    /// <summary>
    /// Optional: audit, invoice, report view/export (no write).
    /// </summary>
    public const string Accountant = "Accountant";

    /// <summary>
    /// Fallback when no role is assigned (e.g. token build). Not a real role; do not use for authorization.
    /// </summary>
    public const string FallbackUnknown = "User";

    /// <summary>
    /// Canonical system roles for seed and non-deletable policy. Admin removed; use SuperAdmin only for top admin.
    /// </summary>
    public static readonly IReadOnlyList<string> Canonical = new[]
    {
        SuperAdmin,
        Manager,
        Cashier,
        Waiter,
        Kitchen,
        ReportViewer,
        Accountant
    };

    /// <summary>
    /// Role names that must not be created as custom roles (merged/removed or legacy).
    /// </summary>
    public static readonly IReadOnlyList<string> ReservedRoleNames = new[]
    {
        "Admin",
        "Administrator",
    };
}
