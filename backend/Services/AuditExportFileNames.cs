using KasseAPI_Final.Tenancy;



namespace KasseAPI_Final.Services;



/// <summary>

/// Canonical audit log export names:

/// <c>audit_{tenantSlug}_{from:yyyyMMdd}_{to:yyyyMMdd}_{yyyyMMdd_HHmmss}.{json|csv}</c>

/// </summary>

public static class AuditExportFileNames

{

    public const string Prefix = "audit";



    public static string Build(

        string? tenantSlug,

        DateTime? fromDate,

        DateTime? toDate,

        string? format,

        DateTime? at = null) =>

        new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(

            Prefix,

            NormalizeExtension(format),

            registerId: ExportFileNameSegments.DateOnly(fromDate),

            additional: ExportFileNameSegments.DateOnly(toDate),

            at: at,

            tenantSlug: tenantSlug);



    public static string NormalizeExtension(string? format) =>

        (format ?? "csv").Trim().ToLowerInvariant() switch

        {

            "json" => "json",

            _ => "csv",

        };



    public static string ContentTypeForFormat(string? format) =>

        NormalizeExtension(format) == "json" ? "application/json" : "text/csv";

}


