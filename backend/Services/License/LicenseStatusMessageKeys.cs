namespace KasseAPI_Final.Services.License;

/// <summary>Stable message keys for mandant license status (de/en/tr via <see cref="LicenseStatusMessages"/>).</summary>
public static class LicenseStatusMessageKeys
{
    public const string Active = "license.status.active";
    public const string ExpiringSoon = "license.status.expiring_soon";
    public const string Grace = "license.status.grace";
    public const string Locked = "license.status.locked";
    public const string None = "license.status.none";
}
