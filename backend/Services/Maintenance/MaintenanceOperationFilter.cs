namespace KasseAPI_Final.Services.Maintenance;

/// <summary>
/// Limited-mode allowlist for platform maintenance: safe reads and critical auth/status
/// paths stay available; high-risk and other writes are blocked for non-SuperAdmin traffic.
/// </summary>
public sealed class MaintenanceOperationFilter
{
    /// <summary>
    /// Explicit write prefixes that must never run during platform maintenance
    /// (POS fiscal, offline ingest, backup trigger, etc.).
    /// </summary>
    private static readonly string[] BlockedWriteKeys =
    [
        "POST /api/pos/payment",
        "POST /api/payment",
        "POST /api/pos/offline-orders",
        "POST /api/pos/offline",
        "POST /api/rksv/special-receipts",
        "POST /api/tagesabschluss",
        "POST /api/admin/backup/trigger",
        "POST /api/admin/backup/restore",
        "POST /api/Receipts",
        "POST /api/receipts",
    ];

    /// <summary>
    /// Additional write prefixes allowed beyond <see cref="Middleware.MaintenanceMiddleware.IsCriticalPath"/>.
    /// </summary>
    private static readonly string[] AllowedWriteKeys =
    [
        "POST /api/Auth/",
        "POST /api/auth/",
        "POST /api/csrf/",
        "POST /api/admin/maintenance-notifications/",
        "POST /api/pos/maintenance-notifications/",
        "POST /api/admin/maintenance/",
        "POST /api/pos/maintenance/",
        "POST /api/maintenance/",
    ];

    /// <summary>
    /// Returns whether <paramref name="method"/> + <paramref name="path"/> may proceed
    /// while platform maintenance is active (limited / read-mostly mode).
    /// </summary>
    public bool IsOperationAllowed(string method, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalizedMethod = (method ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedPath = NormalizePath(path);

        // Health, auth, csrf, maintenance status/ack — always reachable.
        if (Middleware.MaintenanceMiddleware.IsCriticalPath(normalizedPath))
            return true;

        // Limited mode: all safe reads.
        if (IsSafeReadMethod(normalizedMethod))
            return true;

        var key = $"{normalizedMethod} {normalizedPath}";

        // Hard block high-risk mutations even if a broader prefix would allow them.
        if (MatchesAnyPrefix(key, BlockedWriteKeys))
            return false;

        if (MatchesAnyPrefix(key, AllowedWriteKeys))
            return true;

        // Default: block other writes (create/update/delete) during maintenance.
        return false;
    }

    /// <summary>True when the operation is on the explicit high-risk block list.</summary>
    public bool IsHighRiskBlockedWrite(string method, string path)
    {
        var normalizedMethod = (method ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedPath = NormalizePath(path);
        var key = $"{normalizedMethod} {normalizedPath}";
        return MatchesAnyPrefix(key, BlockedWriteKeys);
    }

    private static bool IsSafeReadMethod(string method) =>
        method is "GET" or "HEAD" or "OPTIONS";

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.Length == 0)
            return "/";

        // Strip query string if callers pass a raw URL accidentally.
        var q = trimmed.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
            trimmed = trimmed[..q];

        return trimmed.Length == 0 ? "/" : trimmed;
    }

    private static bool MatchesAnyPrefix(string key, string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
