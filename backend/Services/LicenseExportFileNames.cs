using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Services;

/// <summary>
/// Canonical license export names:
/// <list type="bullet">
/// <item><c>license_{tenantSlug}_{yyyyMMdd_HHmmss}.txt</c> — single license</item>
/// <item><c>licenses_{tenantSlug}_{yyyyMMdd_HHmmss}.{json|csv}</c> — multiple licenses</item>
/// </list>
/// </summary>
public static class LicenseExportFileNames
{
    public const string SinglePrefix = "license";
    public const string MultiplePrefix = "licenses";
    public const string DefaultSlugFallback = "deployment";

    /// <summary>Single license file: <c>license_{slug}_{stamp}.txt</c>.</summary>
    public static string BuildSingle(string? tenantSlug, DateTime? at = null) =>
        new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(
            SinglePrefix,
            "txt",
            at: at,
            tenantSlug: string.IsNullOrWhiteSpace(tenantSlug) ? DefaultSlugFallback : tenantSlug);

    /// <summary>Bulk license export: <c>licenses_{slug}_{stamp}.{json|csv}</c>.</summary>
    public static string BuildMultiple(string? tenantSlug, string? format, DateTime? at = null) =>
        new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(
            MultiplePrefix,
            NormalizeMultipleExtension(format),
            at: at,
            tenantSlug: string.IsNullOrWhiteSpace(tenantSlug) ? DefaultSlugFallback : tenantSlug);

    public static string NormalizeMultipleExtension(string? format) =>
        (format ?? "json").Trim().ToLowerInvariant() switch
        {
            "csv" => "csv",
            _ => "json",
        };

    public static string ContentTypeForMultipleFormat(string? format) =>
        NormalizeMultipleExtension(format) == "csv"
            ? "text/csv; charset=utf-8"
            : "application/json; charset=utf-8";

    public const string SingleContentType = "text/plain; charset=utf-8";
}
