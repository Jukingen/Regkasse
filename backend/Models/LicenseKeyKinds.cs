namespace KasseAPI_Final.Models;

/// <summary>
/// Discriminator for unified REGK keys (<c>REGK-yyyyMMdd-{slug}-{8}</c>).
/// Not the SaaS package tier <see cref="Enums.LicenseType"/>.
/// </summary>
public static class LicenseKeyKinds
{
    public const string System = "system";
    public const string Tenant = "tenant";
    public const string Both = "both";
}
