using System.Net;

namespace KasseAPI_Final.Tenancy;

/// <summary>
/// Hostname helpers for tenant slug extraction and development CORS.
/// Supports <c>*.regkasse.local</c> and other <c>*.local</c> hosts-file dev domains.
/// </summary>
public static class TenantHostNames
{
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
    /// Hosts-file development domains (e.g. <c>cafe.regkasse.local</c>, <c>tenant.example.local</c>).
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
    /// First subdomain label when not admin/www; loopback hosts map to <c>admin</c>.
    /// </summary>
    public static string GetTenantSlugFromHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || IsLoopbackHost(host))
            return "admin";

        var parts = host.Split('.');
        if (parts.Length >= 1)
        {
            var first = parts[0];
            if (!string.Equals(first, "admin", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(first, "www", StringComparison.OrdinalIgnoreCase))
            {
                return first;
            }
        }

        return "admin";
    }
}
