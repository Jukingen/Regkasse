using KasseAPI_Final.Models;

namespace KasseAPI_Final.Configuration;

/// <summary>
/// Two-step gate for critical admin mutations. Bound from <c>CriticalActions</c>.
/// When <see cref="Enabled"/> is false, <see cref="Middleware.CriticalActionMiddleware"/> is a no-op.
/// </summary>
public sealed class CriticalActionOptions
{
    public const string SectionName = "CriticalActions";

    public const string ApprovalHeaderName = "X-Critical-Action-Approval";

    /// <summary>When false, middleware does not enforce approval headers.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// When true and the host is Development, skip enforcement.
    /// Ignored outside Development (fail-closed).
    /// </summary>
    public bool BypassInDevelopment { get; set; } = true;

    /// <summary>Opaque approval token lifetime in minutes (2–30).</summary>
    public int ApprovalTokenTtlMinutes { get; set; } = 5;

    /// <summary>Pending Super Admin approval request lifetime in minutes (5–120).</summary>
    public int PendingRequestTtlMinutes { get; set; } = 30;

    /// <summary>Allow Super Admin to approve their own critical action (issue self token after 2FA).</summary>
    public bool SuperAdminMaySelfApprove { get; set; } = true;

    /// <summary>
    /// Path rules evaluated against authenticated mutating requests.
    /// Empty list uses <see cref="DefaultPathRules"/>.
    /// </summary>
    public List<CriticalActionPathRule> PathRules { get; set; } = [];

    public IReadOnlyList<CriticalActionPathRule> ResolvePathRules() =>
        PathRules.Count > 0 ? PathRules : DefaultPathRules;

    public static IReadOnlyList<CriticalActionPathRule> DefaultPathRules { get; } =
    [
        new()
        {
            Methods = ["POST"],
            PathContains = "/api/rksv/special-receipts/schlussbeleg",
            ActionType = CriticalActionType.SchlussbelegCreation,
        },
        new()
        {
            Methods = ["POST"],
            PathContains = "/schlussbeleg",
            ActionType = CriticalActionType.SchlussbelegCreation,
        },
        new()
        {
            Methods = ["PUT"],
            PathContains = "/decommission",
            ActionType = CriticalActionType.DecommissionRegister,
        },
        new()
        {
            Methods = ["DELETE"],
            PathContains = "/api/admin/tenants/",
            PathEndsWith = "/permanent",
            ActionType = CriticalActionType.TenantDeletion,
        },
        new()
        {
            Methods = ["DELETE"],
            PathContains = "/api/admin/tenants/",
            PathEndsWith = "/hard",
            ActionType = CriticalActionType.TenantDeletion,
        },
        new()
        {
            Methods = ["DELETE"],
            PathContains = "/api/admin/tenants/",
            // Soft-delete archive: DELETE /api/admin/tenants/{guid} (no extra segment)
            PathMatchesTenantRootDelete = true,
            ActionType = CriticalActionType.TenantArchive,
        },
        new()
        {
            Methods = ["PUT", "POST"],
            PathContains = "/api/admin/tenants/",
            PathContainsSecondary = "/license",
            ActionType = CriticalActionType.LicenseChange,
        },
        new()
        {
            Methods = ["POST"],
            PathContains = "/api/admin/tenant-settings/request",
            ActionType = CriticalActionType.CurrencyChange,
            // CountryChange shares this endpoint; middleware tags CurrencyChange as the gate kind.
            // Controllers still enforce four-eyes per setting type.
        },
        new()
        {
            Methods = ["POST"],
            PathContains = "/api/admin/products/deactivate-all",
            ActionType = CriticalActionType.DeleteAllProducts,
        },
        new()
        {
            Methods = ["PUT"],
            PathContains = "/api/admin/backup/settings",
            ActionType = CriticalActionType.BackupDisable,
        },
        new()
        {
            Methods = ["DELETE"],
            PathContains = "/api/admin/rksv/dep-export/schedule/",
            ActionType = CriticalActionType.FiscalExportDelete,
        },
        new()
        {
            Methods = ["PUT", "POST", "PATCH"],
            PathContains = "/api/admin/users/",
            PathContainsSecondary = "/roles",
            ActionType = CriticalActionType.UserRoleChange,
        },
        new()
        {
            Methods = ["PUT", "POST"],
            PathContains = "/api/admin/access/roles",
            ActionType = CriticalActionType.MassPermissionUpdate,
        },
        new()
        {
            Methods = ["PUT", "POST"],
            PathContains = "/api/admin/roles/",
            PathContainsSecondary = "/permissions",
            ActionType = CriticalActionType.MassPermissionUpdate,
        },
    ];
}

/// <summary>Maps an HTTP method + path fragment to a <see cref="CriticalActionType"/>.</summary>
public sealed class CriticalActionPathRule
{
    /// <summary>HTTP methods (uppercase). Empty = any mutating method.</summary>
    public List<string> Methods { get; set; } = [];

    /// <summary>Case-insensitive substring that must appear in the path.</summary>
    public string PathContains { get; set; } = string.Empty;

    /// <summary>Optional second substring (e.g. "/license" after tenant id).</summary>
    public string? PathContainsSecondary { get; set; }

    /// <summary>Optional case-insensitive path suffix.</summary>
    public string? PathEndsWith { get; set; }

    /// <summary>
    /// When true, matches DELETE /api/admin/tenants/{guid} with no further path segments.
    /// </summary>
    public bool PathMatchesTenantRootDelete { get; set; }

    public CriticalActionType ActionType { get; set; }
}
