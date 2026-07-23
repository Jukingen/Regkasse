using System.Globalization;
using System.Text.RegularExpressions;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Canonical backup artifact names:
/// <c>backup_{tenantSlug}_{strategy}_{yyyyMMdd_HHmmss}[_{sizeHint}].{extension}</c>
/// Examples: <c>backup_cafe_tenant_20260722_143022.tenant.zip</c>,
/// <c>backup_cafe_system_20260722_143022.system.zip</c>.
/// </summary>
public static partial class BackupArtifactFileNameBuilder
{
    private const string UnknownSlug = "unknown";

    public static string FormatTimestampUtc(DateTime timestampUtc) =>
        timestampUtc.ToUniversalTime().ToString("yyyyMMdd_HHmmss");

    public static string StrategyLabel(BackupStrategyKind strategy) =>
        strategy == BackupStrategyKind.Tenant ? "tenant" : "system";

    /// <summary>
    /// Compact size hint for download names (e.g. <c>12mb</c>, <c>450kb</c>). Null when size is unknown/empty.
    /// </summary>
    public static string? FormatSizeHint(long? byteSize)
    {
        if (byteSize is null or <= 0)
            return null;

        var bytes = byteSize.Value;
        if (bytes < 1024)
            return $"{bytes}b";

        double kb = bytes / 1024d;
        if (kb < 1024)
            return $"{Math.Max(1, (int)Math.Round(kb))}kb";

        double mb = kb / 1024d;
        if (mb < 1024)
            return FormatDecimalHint(mb, "mb");

        double gb = mb / 1024d;
        return FormatDecimalHint(gb, "gb");
    }

    /// <summary>Core builder: <c>backup_{slug}_{strategy}_{stamp}[_{size}].{ext}</c>.</summary>
    public static string Build(
        string? tenantSlug,
        BackupStrategyKind strategy,
        string extension,
        DateTime timestampUtc,
        long? byteSize = null)
    {
        var slug = SanitizeSlug(tenantSlug);
        var strategyLabel = StrategyLabel(strategy);
        var stamp = FormatTimestampUtc(timestampUtc);
        var ext = NormalizeExtension(extension);
        var size = FormatSizeHint(byteSize);
        var sizePart = size is null ? string.Empty : $"_{size}";
        return $"backup_{slug}_{strategyLabel}_{stamp}{sizePart}.{ext}";
    }

    public static string BuildLogicalDumpFileName(
        string tenantSlug,
        DateTime timestampUtc,
        BackupStrategyKind strategy = BackupStrategyKind.System,
        long? byteSize = null) =>
        Build(tenantSlug, strategy, "dump", timestampUtc, byteSize);

    /// <summary>Tenant-scoped JSON ZIP package (not pg_dump custom format).</summary>
    public static string BuildTenantLogicalPackageFileName(
        string tenantSlug,
        DateTime timestampUtc,
        long? byteSize = null) =>
        Build(tenantSlug, BackupStrategyKind.Tenant, "tenant.zip", timestampUtc, byteSize);

    /// <summary>Tenant incremental (delta) JSON ZIP — not a standalone restore artifact.</summary>
    public static string BuildTenantIncrementalPackageFileName(
        string tenantSlug,
        DateTime timestampUtc,
        long? byteSize = null) =>
        Build(tenantSlug, BackupStrategyKind.Tenant, "tenant.incr.zip", timestampUtc, byteSize);

    /// <summary>Super Admin system JSON ZIP (Identity + nested tenant packages).</summary>
    public static string BuildSystemPackageFileName(
        string tenantSlug,
        DateTime timestampUtc,
        long? byteSize = null) =>
        Build(tenantSlug, BackupStrategyKind.System, "system.zip", timestampUtc, byteSize);

    public static string BuildManifestFileName(
        string tenantSlug,
        DateTime timestampUtc,
        BackupStrategyKind strategy = BackupStrategyKind.System)
    {
        var slug = SanitizeSlug(tenantSlug);
        var strategyLabel = StrategyLabel(strategy);
        var stamp = FormatTimestampUtc(timestampUtc);
        return $"backup_{slug}_{strategyLabel}_{stamp}_manifest.json";
    }

    /// <summary>
    /// Inserts a size hint before the compound extension when missing
    /// (download-friendly; on-disk storage names typically omit size).
    /// </summary>
    public static string InsertSizeHint(string fileName, long? byteSize)
    {
        var hint = FormatSizeHint(byteSize);
        if (hint is null || string.IsNullOrWhiteSpace(fileName))
            return fileName;

        var name = Path.GetFileName(fileName.Trim());
        if (string.IsNullOrEmpty(name))
            return fileName;

        const string manifestSuffix = "_manifest.json";
        if (name.EndsWith(manifestSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var stem = name[..^manifestSuffix.Length];
            if (stem.EndsWith($"_{hint}", StringComparison.OrdinalIgnoreCase))
                return name;
            return $"{stem}_{hint}{manifestSuffix}";
        }

        foreach (var ext in KnownExtensions())
        {
            if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                continue;

            var stem = name[..^ext.Length];
            if (stem.EndsWith($"_{hint}", StringComparison.OrdinalIgnoreCase))
                return name;

            return $"{stem}_{hint}{ext}";
        }

        return $"{name}_{hint}";
    }

    /// <summary>Exposes slug sanitization for nested system package entry names.</summary>
    public static string SanitizeSlugPublic(string? tenantSlug) => SanitizeSlug(tenantSlug);

    internal static string SanitizeSlug(string? tenantSlug)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug))
            return UnknownSlug;

        var trimmed = tenantSlug.Trim().ToLowerInvariant();
        var sanitized = InvalidSlugChars().Replace(trimmed, "_");
        return string.IsNullOrEmpty(sanitized) ? UnknownSlug : sanitized;
    }

    private static string NormalizeExtension(string extension)
    {
        var ext = (extension ?? string.Empty).Trim().TrimStart('.');
        return string.IsNullOrEmpty(ext) ? "bin" : ext.ToLowerInvariant();
    }

    private static string FormatDecimalHint(double value, string unit)
    {
        var rounded = Math.Round(value, value >= 10 ? 0 : 1, MidpointRounding.AwayFromZero);
        var text = rounded.ToString(rounded >= 10 || Math.Abs(rounded % 1) < 0.05 ? "0" : "0.#", CultureInfo.InvariantCulture);
        return $"{text}{unit}";
    }

    private static string[] KnownExtensions() =>
    [
        ".tenant.incr.zip",
        ".tenant.zip",
        ".system.zip",
        ".dump",
        ".zip",
        ".json",
    ];

    [GeneratedRegex(@"[^a-z0-9_-]", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidSlugChars();
}
