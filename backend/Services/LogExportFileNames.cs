using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Services;

/// <summary>
/// Canonical error/application log export names:
/// <c>log_{tenantSlug}_{yyyyMMdd_HHmmss}.{txt|csv|json}</c>
/// </summary>
public static class LogExportFileNames
{
    public const string Prefix = "log";
    public const string DefaultSlugFallback = "deployment";

    public static string Build(string? tenantSlug, string? format, DateTime? at = null) =>
        new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(
            Prefix,
            NormalizeExtension(format),
            at: at,
            tenantSlug: string.IsNullOrWhiteSpace(tenantSlug) ? DefaultSlugFallback : tenantSlug);

    public static string NormalizeExtension(string? format) =>
        (format ?? "txt").Trim().ToLowerInvariant() switch
        {
            "csv" => "csv",
            "json" => "json",
            _ => "txt",
        };

    public static string ContentTypeForFormat(string? format) =>
        NormalizeExtension(format) switch
        {
            "csv" => "text/csv; charset=utf-8",
            "json" => "application/json; charset=utf-8",
            _ => "text/plain; charset=utf-8",
        };
}
