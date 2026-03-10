namespace KasseAPI_Final.Authorization;

/// <summary>
/// Canonical role names for POS authorization. Use these constants instead of magic strings.
/// Identity (AspNetRoles) and JWT use the same names.
/// </summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
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
    /// Canonical system roles for policy and assignments. Obsolete names (Administrator, Kellner, BranchManager, Auditor, Demo) are not included; use migration/reassignment for existing DBs.
    /// </summary>
    public static readonly IReadOnlyList<string> Canonical = new[]
    {
        SuperAdmin,
        Admin,
        Manager,
        Cashier,
        Waiter,
        Kitchen,
        ReportViewer,
        Accountant
    };
}
