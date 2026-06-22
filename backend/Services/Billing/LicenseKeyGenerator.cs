using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using KasseAPI_Final.Services.AdminTenants;

namespace KasseAPI_Final.Services.Billing;

public interface ILicenseKeyGenerator
{
    string GenerateLicenseKey(string tenantSlug, DateTime validUntil);
    bool ValidateLicenseKeyFormat(string licenseKey);
}

/// <summary>
/// Mandanten SaaS billing key format: <c>REGK-{yyyyMMdd}-{tenantSlug}-{random}</c>
/// (e.g. <c>REGK-20261231-cafe-A7F3K2D9</c>). Distinct from deployment JWT display keys.
/// </summary>
public sealed partial class LicenseKeyGenerator : ILicenseKeyGenerator
{
    public const string InvalidFormatMessage =
        "Invalid license key format. Expected REGK-YYYYMMDD-{tenantSlug}-{code}.";

    private const int RandomSuffixLength = 8;
    private const string RandomAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    [GeneratedRegex(
        @"^[A-Z0-9]{8}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex RandomSuffixRegex();

    public string GenerateLicenseKey(string tenantSlug, DateTime validUntil)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug))
            throw new ArgumentException("Tenant slug is required.", nameof(tenantSlug));

        var slug = TenantSlugSuggestions.NormalizeSlug(tenantSlug);
        if (string.IsNullOrEmpty(slug))
            throw new ArgumentException("Tenant slug is required.", nameof(tenantSlug));

        var validUntilUtc = validUntil.Kind switch
        {
            DateTimeKind.Utc => validUntil,
            DateTimeKind.Local => validUntil.ToUniversalTime(),
            _ => DateTime.SpecifyKind(validUntil, DateTimeKind.Utc),
        };

        var datePart = validUntilUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var randomPart = GenerateRandomSuffix();
        return $"REGK-{datePart}-{slug}-{randomPart}";
    }

    public bool ValidateLicenseKeyFormat(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return false;

        var parts = licenseKey.Trim().Split('-');
        if (parts.Length < 4)
            return false;

        if (!parts[0].Equals("REGK", StringComparison.OrdinalIgnoreCase))
            return false;

        var datePart = parts[1];
        if (datePart.Length != 8
            || !DateTime.TryParseExact(
                datePart,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            return false;
        }

        var randomPart = parts[^1];
        if (!RandomSuffixRegex().IsMatch(randomPart))
            return false;

        var slug = string.Join('-', parts.AsSpan(2, parts.Length - 3));
        return TenantSlugSuggestions.IsValidSlug(slug);
    }

    private static string GenerateRandomSuffix()
    {
        Span<char> buffer = stackalloc char[RandomSuffixLength];
        Span<byte> randomBytes = stackalloc byte[RandomSuffixLength];

        RandomNumberGenerator.Fill(randomBytes);
        for (var i = 0; i < RandomSuffixLength; i++)
            buffer[i] = RandomAlphabet[randomBytes[i] % RandomAlphabet.Length];

        return new string(buffer);
    }
}
