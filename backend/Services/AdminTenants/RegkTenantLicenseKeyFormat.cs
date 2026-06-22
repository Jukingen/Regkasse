using System.Text.RegularExpressions;

namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>REGK display key validation (aligned with <see cref="LicenseService"/> activation format).</summary>
public static partial class RegkTenantLicenseKeyFormat
{
    [GeneratedRegex(
        @"^REGK-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LicenseKeyRegex();

    public const string InvalidFormatMessage =
        "Invalid license key format. Expected REGK-XXXXX-XXXXX-XXXXX.";

    public static bool IsValid(string? licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return false;
        return LicenseKeyRegex().IsMatch(licenseKey.Trim());
    }
}
