namespace KasseAPI_Final.Services.License;

/// <summary>
/// Single format gate for unified <c>REGK-{yyyyMMdd}-{slug}-{8}</c> keys and legacy
/// <c>REGK-XXXXX-XXXXX-XXXXX</c> deployment display keys. Delegates to
/// <see cref="KasseAPI_Final.Services.Billing.LicenseKeyGenerator"/>.
/// </summary>
public interface ILicenseKeyValidator
{
    LicenseKeyParseResult Parse(string? licenseKey);

    bool IsValidFormat(string? licenseKey);

    bool IsSystemLicense(string? licenseKey);

    bool IsTenantLicense(string? licenseKey);

    string Normalize(string? licenseKey);
}
