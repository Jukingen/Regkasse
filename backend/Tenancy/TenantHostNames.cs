using System.Net;

namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Hostname helpers for tenant slug extraction and development CORS.
/// Supports <c>*.regkasse.local</c> and other <c>*.local</c> hosts-file dev domains.
/// </summary>
public static class TenantHostNames
{
    /// <summary>
    /// Shared platform host labels that must never be treated as mandant slugs
    /// (<c>pos.regkasse.at</c>, <c>api.regkasse.at</c>, <c>admin.regkasse.at</c>, <c>www</c>).
    /// </summary>
    public static bool IsReservedPlatformHostLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        return string.Equals(label, "admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "www", StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "pos", StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "api", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc cref="IsLoopbackHost"/>
    public static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (host.StartsWith("127.0.0.1", StringComparison.Ordinal))
            return true;

        return IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
    }

    /// <summary>
    /// Hosts-file development domains (e.g. <c>dev.regkasse.local</c>, <c>tenant.example.local</c>).
    /// </summary>
    public static bool IsLocalDevelopmentDomain(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        return host.EndsWith(".regkasse.local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Development CORS: loopback, LAN RFC1918 (checked separately), and <c>*.local</c> dev hosts.
    /// </summary>
    public static bool IsTrustedLocalDevCorsHost(string? host)
    {
        return IsLoopbackHost(host) || IsLocalDevelopmentDomain(host);
    }

    /// <summary>
    /// Production CORS: platform web surfaces under <c>regkasse.at</c> (POS, FA, Sites, tenant slug hosts).
    /// Custom <see cref="Models.TenantDomain"/> hosts must still be listed in <c>Cors:AllowedOrigins</c>.
    /// </summary>
    public static bool IsRegkassePlatformWebOriginHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        return host.Equals("regkasse.at", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".regkasse.at", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Shared API/POS/FA hosts (and loopback) must not receive a pre-auth ambient tenant in Production —
    /// authenticated traffic is scoped solely by JWT <c>tenant_id</c>.
    /// Custom <see cref="Models.TenantDomain"/> hosts and mandant subdomains still bind from Host.
    /// </summary>
    public static bool ShouldSkipPreAuthHostBinding(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || IsLoopbackHost(host))
            return true;

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 1 && IsReservedPlatformHostLabel(parts[0]);
    }

    /// <summary>
    /// First subdomain label when not a reserved platform host; loopback and reserved hosts map to <c>admin</c>
    /// (platform ambient binding — JWT <c>tenant_id</c> is authoritative after login for POS/API).
    /// </summary>
    public static string GetTenantSlugFromHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || IsLoopbackHost(host))
            return "admin";

        var parts = host.Split('.');
        if (parts.Length >= 1)
        {
            var first = parts[0];
            if (!IsReservedPlatformHostLabel(first))
                return first;
        }

        return "admin";
    }
}
