using KasseAPI_Final.Tenancy;



namespace KasseAPI_Final.Services.DataExport;



/// <summary>

/// Canonical GDPR data-export ZIP names:

/// <c>data-export_{tenantSlug}_{yyyyMMdd_HHmmss}.zip</c>

/// </summary>

public static class DataExportFileNames

{

    public const string Prefix = "data-export";

    public const string ManifestZipEntryName = "manifest.json";



    public static string Build(string? tenantSlug, DateTime? at = null) =>

        new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(

            Prefix,

            "zip",

            at: at,

            tenantSlug: tenantSlug);

}



/// <summary>ZIP-sidecar metadata for a GDPR export package.</summary>

public sealed class DataExportManifest

{

    public required Guid ExportId { get; init; }

    public required string TenantSlug { get; init; }

    public required DateTime ExportedAt { get; init; }

    public required string FileName { get; init; }

}


