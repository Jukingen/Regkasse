using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Http;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Maps HTTP path + verb to required <see cref="LicenseFeatureIds"/> entries for authenticated traffic.
/// Anonymous and health/license read endpoints are handled by the caller (no gate).
/// </summary>
public static class LicensePathFeatureEvaluator
{
    /// <summary>Returns distinct feature ids required for this request, or empty when no license feature gate applies.</summary>
    public static IReadOnlyList<string> GetRequiredFeatures(PathString path, string method)
    {
        if (!path.HasValue)
            return Array.Empty<string>();

        var v = path.Value!;
        if (v.Length == 0)
            return Array.Empty<string>();

        var m = (method ?? "GET").Trim().ToUpperInvariant();

        if (v.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        if (v.StartsWith("/api/license", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        if (v.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "/health", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "/api/health", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        if (v.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        if (v.StartsWith("/api/rksv/special-receipts", StringComparison.OrdinalIgnoreCase))
            return new[] { LicenseFeatureIds.AdminBasic, LicenseFeatureIds.AdminRksv };

        if (v.StartsWith("/api/admin/", StringComparison.OrdinalIgnoreCase))
        {
            var list = new List<string> { LicenseFeatureIds.AdminBasic };
            if (RequiresAdminLicenseManage(v, m))
                list.Add(LicenseFeatureIds.AdminLicenseManage);
            if (RequiresAdminRksv(v))
                list.Add(LicenseFeatureIds.AdminRksv);
            return Deduplicate(list);
        }

        if (v.StartsWith("/api/offline-transactions", StringComparison.OrdinalIgnoreCase))
            return new[] { LicenseFeatureIds.PosOffline };

        if (IsPosFiscalPath(v))
            return new[] { LicenseFeatureIds.PosFiscal };

        return Array.Empty<string>();
    }

    /// <summary>
    /// When <paramref name="appContext"/> is set, skip required features that belong to the other app
    /// (defense-in-depth without double-blocking cross-context misrouting).
    /// </summary>
    public static bool ShouldEnforceFeature(string featureId, string? appContext)
    {
        if (string.IsNullOrWhiteSpace(appContext))
            return true;

        if (featureId.StartsWith("pos_", StringComparison.OrdinalIgnoreCase))
            return string.Equals(appContext, "pos", StringComparison.OrdinalIgnoreCase);

        if (featureId.StartsWith("admin_", StringComparison.OrdinalIgnoreCase))
            return string.Equals(appContext, "admin", StringComparison.OrdinalIgnoreCase);

        return true;
    }

    public static string? ReadAppContext(HttpContext context) =>
        context.User.FindFirst(ClientAppPolicy.AppContextClaimType)?.Value;

    private static bool IsPosFiscalPath(string v)
    {
        if (v.StartsWith("/api/pos/", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/payment", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/Payment", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/invoice", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/Invoice", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/receipts", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/Receipts", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/tse", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/Tse", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/tagesabschluss", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/Tagesabschluss", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/finanzonline", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/FinanzOnline", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/rksv/", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/cart", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.StartsWith("/api/Cart", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static bool RequiresAdminRksv(string v) =>
        v.StartsWith("/api/admin/fiscal-export", StringComparison.OrdinalIgnoreCase)
        || v.StartsWith("/api/admin/rksv", StringComparison.OrdinalIgnoreCase)
        || v.StartsWith("/api/admin/audit", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresAdminLicenseManage(string v, string m)
    {
        if (v.StartsWith("/api/admin/licenses", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!v.StartsWith("/api/admin/license", StringComparison.OrdinalIgnoreCase))
            return false;

        if (m is not ("POST" or "PUT" or "DELETE"))
            return false;

        // Operator activation stays under settings.manage RBAC; license *issuance* is gated separately.
        if (v.Contains("/activate", StringComparison.OrdinalIgnoreCase))
            return false;

        return v.Contains("/generate", StringComparison.OrdinalIgnoreCase)
               || v.Contains("/upgrade", StringComparison.OrdinalIgnoreCase)
               || v.Contains("/renew", StringComparison.OrdinalIgnoreCase)
               || v.Contains("/transfer", StringComparison.OrdinalIgnoreCase)
               || v.Contains("/revoke", StringComparison.OrdinalIgnoreCase)
               || v.Contains("/extend", StringComparison.OrdinalIgnoreCase)
               || v.Contains("/unregister-machine", StringComparison.OrdinalIgnoreCase)
               || v.Contains("/cancel", StringComparison.OrdinalIgnoreCase)
               || v.Contains("/soft-delete", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> Deduplicate(List<string> raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in raw)
        {
            if (!string.IsNullOrWhiteSpace(s))
                set.Add(s);
        }

        return set.OrderBy(s => s, StringComparer.Ordinal).ToArray();
    }
}
