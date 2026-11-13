using KasseAPI_Final.Tenancy;



namespace KasseAPI_Final.Services;



/// <summary>

/// Canonical voucher export names:

/// <c>voucher_{tenantSlug}_{yyyyMMdd_HHmmss}.{json|csv}</c>

/// </summary>

public static class VoucherExportFileNames

{

    public const string Prefix = "voucher";



    public static string Build(string? tenantSlug, string? format, DateTime? at = null) =>

        new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(

            Prefix,

            NormalizeExtension(format),

            at: at,

            tenantSlug: tenantSlug);



    public static string NormalizeExtension(string? format) =>

        (format ?? "json").Trim().ToLowerInvariant() switch

        {

            "csv" => "csv",

            _ => "json",

        };



    public static string ContentTypeForFormat(string? format) =>

        NormalizeExtension(format) == "csv" ? "text/csv" : "application/json";

}


