using System.Text.RegularExpressions;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Structured backup artifact names: <c>backup_{tenantSlug}_{yyyyMMdd_HHmmss}.dump</c> and matching manifest.
/// </summary>
public static partial class BackupArtifactFileNameBuilder
{
    private const string UnknownSlug = "unknown";

    public static string FormatTimestampUtc(DateTime timestampUtc) =>
        timestampUtc.ToUniversalTime().ToString("yyyyMMdd_HHmmss");

    public static string BuildLogicalDumpFileName(string tenantSlug, DateTime timestampUtc) =>
        $"backup_{SanitizeSlug(tenantSlug)}_{FormatTimestampUtc(timestampUtc)}.dump";

    public static string BuildManifestFileName(string tenantSlug, DateTime timestampUtc) =>
        $"backup_{SanitizeSlug(tenantSlug)}_{FormatTimestampUtc(timestampUtc)}_manifest.json";

    internal static string SanitizeSlug(string? tenantSlug)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug))
            return UnknownSlug;

        var trimmed = tenantSlug.Trim().ToLowerInvariant();
        var sanitized = InvalidSlugChars().Replace(trimmed, "_");
        return string.IsNullOrEmpty(sanitized) ? UnknownSlug : sanitized;
    }

    [GeneratedRegex(@"[^a-z0-9_-]", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidSlugChars();
}
