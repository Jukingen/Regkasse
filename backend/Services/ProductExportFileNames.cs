using KasseAPI_Final.Tenancy;



namespace KasseAPI_Final.Services;



/// <summary>

/// Canonical product export names:

/// <c>product_{tenantSlug}_{yyyyMMdd_HHmmss}.{csv|json}</c>

/// </summary>

public static class ProductExportFileNames

{

    public const string Prefix = "product";



    public static string Build(string? tenantSlug, string? format, DateTime? at = null) =>

        new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(

            Prefix,

            NormalizeExtension(format),

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


