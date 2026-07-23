using KasseAPI_Final.Tenancy;



namespace KasseAPI_Final.Services;



/// <summary>

/// Canonical fiscal export download names (via <see cref="IFileNamingService"/>).

/// Base: <c>fiscal-export_{tenantSlug}_{registerNumber}_{yyyyMMdd_HHmmss}.{ext}</c>

/// With profile: <c>fiscal-export_{tenantSlug}_{registerNumber}_{profile}_{yyyyMMdd_HHmmss}.{ext}</c>

/// </summary>

public static class FiscalExportFileNames

{

    public const string Prefix = "fiscal-export";



    public static string Build(

        string? tenantSlug,

        string? registerNumber,

        string? profileName = null,

        string extension = "json",

        DateTime? at = null) =>

        new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(

            Prefix,

            NormalizeExtension(extension),

            registerId: registerNumber,

            additional: profileName,

            at: at,

            tenantSlug: tenantSlug);



    /// <summary>

    /// Profile-only variant when register is unavailable:

    /// <c>fiscal-export_{tenantSlug}_{profileName}_{stamp}.{ext}</c>

    /// </summary>

    public static string BuildWithProfile(

        string? tenantSlug,

        string? profileName,

        string extension = "json",

        DateTime? at = null) =>

        new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(

            Prefix,

            NormalizeExtension(extension),

            additional: profileName,

            at: at,

            tenantSlug: tenantSlug);



    private static string NormalizeExtension(string extension)

    {

        var ext = (extension ?? string.Empty).Trim().TrimStart('.');

        return string.IsNullOrEmpty(ext) ? "json" : ext.ToLowerInvariant();

    }

}


