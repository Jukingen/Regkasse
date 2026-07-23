using KasseAPI_Final.Tenancy;



namespace KasseAPI_Final.Services.Rksv;



/// <summary>

/// Canonical download / storage file names for RKSV §7 DEP JSON exports.

/// Format: <c>dep-export_{tenantSlug}_{registerNumber}_{yyyyMMdd_HHmmss}.json</c>

/// </summary>

public static class RksvDepExportFileNames

{

    public const string Prefix = "dep-export";



    /// <summary>

    /// Builds a filesystem-safe DEP export file name.

    /// Uses local wall-clock time when <paramref name="at"/> is omitted (matches browser download stamps).

    /// </summary>

    public static string Build(string? tenantSlug, string? registerNumber, DateTime? at = null) =>

        new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(

            Prefix,

            "json",

            registerId: registerNumber,

            at: at,

            tenantSlug: tenantSlug);



    /// <summary>Delegates to <see cref="ExportFileNameSegments.Sanitize"/>.</summary>

    internal static string SanitizeSegment(string? value, string fallback) =>

        ExportFileNameSegments.Sanitize(value, fallback);

}


