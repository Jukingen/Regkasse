using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Billing;

namespace KasseAPI_Final.Services.License;

/// <summary>Thin wrapper over <see cref="LicenseKeyGenerator"/> — do not add a second parser.</summary>
public sealed class LicenseKeyValidator : ILicenseKeyValidator
{
    public static readonly LicenseKeyValidator Instance = new();

    public LicenseKeyParseResult Parse(string? licenseKey)
    {
        var input = (licenseKey ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(input))
        {
            return LicenseKeyParseResult.Invalid(
                input,
                "licenseKey is required.");
        }

        if (LicenseKeyGenerator.TryParseLicenseKey(input, out var slug, out var validUntil, out var randomPart))
        {
            var normalized = LicenseKeyGenerator.FormatUnifiedLicenseKey(validUntil, slug, randomPart);
            var isSystem = string.Equals(slug, LicenseKeyGenerator.SystemSlug, StringComparison.Ordinal);
            return new LicenseKeyParseResult
            {
                IsValid = true,
                Normalized = normalized,
                Slug = slug,
                ValidUntilUtc = DateTime.SpecifyKind(validUntil.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc),
                RandomPart = randomPart.ToUpperInvariant(),
                IsSystem = isSystem,
                IsTenant = !isSystem,
            };
        }

        if (RegkTenantLicenseKeyFormat.IsValid(input))
        {
            return new LicenseKeyParseResult
            {
                IsValid = true,
                Normalized = input.ToUpperInvariant(),
                IsSystem = true,
                IsTenant = false,
                IsLegacyDisplay = true,
            };
        }

        return LicenseKeyParseResult.Invalid(input, LicenseKeyGenerator.InvalidFormatMessage);
    }

    public bool IsValidFormat(string? licenseKey) => Parse(licenseKey).IsValid;

    public bool IsSystemLicense(string? licenseKey)
    {
        var parsed = Parse(licenseKey);
        return parsed.IsValid && parsed.IsSystem;
    }

    public bool IsTenantLicense(string? licenseKey)
    {
        var parsed = Parse(licenseKey);
        return parsed.IsValid && parsed.IsTenant;
    }

    public string Normalize(string? licenseKey)
    {
        var parsed = Parse(licenseKey);
        return parsed.IsValid ? parsed.Normalized : (licenseKey ?? string.Empty).Trim();
    }
}
