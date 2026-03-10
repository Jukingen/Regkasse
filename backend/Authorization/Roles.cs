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
    /// Membership here defines system-role behavior (immutable, matrix-only, not deletable) via RoleManagementService.IsSystemRole.
    /// Current list is preserved for backward compatibility — not yet the final minimized POS taxonomy (e.g. Operator/Backoffice/Kitchen).
    /// Removing or reclassifying roles (e.g. Manager) requires a dedicated migration + matrix follow-up; do not shrink this list ad hoc in a small PR.
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
    /// Names that must not be used for custom roles (merged/removed or legacy). Demo reserved: use IsDemo flag only.
    /// </summary>
    public static readonly IReadOnlyList<string> ReservedRoleNames = new[]
    {
        "Admin",
        "Administrator",
        "Demo",
    };
}
