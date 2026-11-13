using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AdminTenants;

namespace KasseAPI_Final.Services.Billing;

public interface ILicenseKeyGenerator
{
    string GenerateLicenseKey(string tenantSlug, DateTime validUntil);
    string GenerateUnifiedLicenseKey(DateTime validUntil, string slug);
    bool ValidateLicenseKeyFormat(string licenseKey);
    bool IsMandantBillingLicenseKey(string licenseKey);
    (string? TenantSlug, DateTime? ValidUntil, string? RandomPart) ParseLicenseKey(string licenseKey);
}

/// <summary>
/// Unified REGK key format: <c>REGK-{yyyyMMdd}-{slug}-{8 alnum}</c>.
/// Mandant keys use the tenant slug; deployment keys use <see cref="SystemSlug"/>.
/// </summary>
public sealed partial class LicenseKeyGenerator : ILicenseKeyGenerator
{
    public const string SystemSlug = LicenseKeyKinds.System;

    public const string InvalidFormatMessage =
        "Invalid license key format. Expected REGK-YYYYMMDD-{tenantSlug}-{code}.";

    public const string ReservedOrInvalidSlugMessage =
        "License slug is reserved or invalid. Use a valid tenant slug or 'system' for server licenses.";

    public const string ExpiryMustBeInTheFutureMessage =
        "License expiry date must be in the future (UTC).";

    public const string InvalidRandomPartMessage =
        "Random part must be 8 alphanumeric characters.";

    private const int RandomSuffixLength = 8;
    private const string RandomAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    [GeneratedRegex(
        @"^[A-Z0-9]{8}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RandomSuffixRegex();

    public string GenerateLicenseKey(string tenantSlug, DateTime validUntil) =>
        GenerateUnifiedLicenseKey(validUntil, tenantSlug);

    public string GenerateUnifiedLicenseKey(DateTime validUntil, string slug)
    {
        var validUntilUtc = ToUtc(validUntil);
        if (validUntilUtc <= DateTime.UtcNow)
            throw new ArgumentException(ExpiryMustBeInTheFutureMessage, nameof(validUntil));

        var randomPart = GenerateRandomString(RandomSuffixLength);
        return FormatUnifiedLicenseKey(validUntilUtc, slug, randomPart);
    }

    /// <summary>
    /// Builds <c>REGK-{yyyyMMdd}-{slug}-{random}</c> after validating slug and random part.
    /// Does not require <paramref name="validUntil"/> to be in the future so expired keys stay well-formed.
    /// </summary>
    public static string FormatUnifiedLicenseKey(DateTime validUntil, string slug, string randomPart)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("License slug is required.", nameof(slug));

        var normalizedSlug = NormalizeKeySlug(slug);
        if (string.IsNullOrEmpty(normalizedSlug))
            throw new ArgumentException("License slug is required.", nameof(slug));

        if (!IsAllowedKeySlug(normalizedSlug))
            throw new ArgumentException(ReservedOrInvalidSlugMessage, nameof(slug));

        if (!IsValidRandomPart(randomPart))
            throw new ArgumentException(InvalidRandomPartMessage, nameof(randomPart));

        var validUntilUtc = ToUtc(validUntil);
        var datePart = validUntilUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return $"REGK-{datePart}-{normalizedSlug}-{randomPart.ToUpperInvariant()}";
    }

    public bool ValidateLicenseKeyFormat(string licenseKey) =>
        TryParseLicenseKey(licenseKey, out _, out _, out _);

    public bool IsMandantBillingLicenseKey(string licenseKey) =>
        IsMandantBillingKey(licenseKey);

    public (string? TenantSlug, DateTime? ValidUntil, string? RandomPart) ParseLicenseKey(string licenseKey)
    {
        if (!TryParseLicenseKey(licenseKey, out var slug, out var validUntil, out var randomPart))
            return (null, null, null);

        return (slug, validUntil, randomPart);
    }

    /// <summary>Legacy display key or unified deployment key (<c>system</c> slug).</summary>
    public static bool IsDeploymentLicenseKey(string? licenseKey) =>
        RegkTenantLicenseKeyFormat.IsValid(licenseKey) || IsSystemLicenseKey(licenseKey);

    public static bool IsSystemLicenseKey(string? licenseKey) =>
        TryParseLicenseKey(licenseKey, out var slug, out _, out _)
        && string.Equals(slug, SystemSlug, StringComparison.Ordinal);

    public static bool IsMandantBillingKey(string? licenseKey) =>
        TryParseLicenseKey(licenseKey, out var slug, out _, out _)
        && !string.Equals(slug, SystemSlug, StringComparison.Ordinal);

    public static bool IsAllowedKeySlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return false;

        if (string.Equals(slug, SystemSlug, StringComparison.OrdinalIgnoreCase))
            return true;

        return TenantSlugSuggestions.IsValidSlug(slug);
    }

    public static bool IsValidRandomPart(string? randomPart) =>
        !string.IsNullOrWhiteSpace(randomPart) && RandomSuffixRegex().IsMatch(randomPart);

    public static bool TryParseLicenseKey(
        string? licenseKey,
        out string slug,
        out DateTime validUntil,
        out string randomPart)
    {
        slug = string.Empty;
        validUntil = default;
        randomPart = string.Empty;

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
                out validUntil))
        {
            return false;
        }

        randomPart = parts[^1];
        if (!IsValidRandomPart(randomPart))
            return false;

        slug = string.Join('-', parts.AsSpan(2, parts.Length - 3)).ToLowerInvariant();
        return IsAllowedKeySlug(slug);
    }

    private static DateTime ToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private static string NormalizeKeySlug(string tenantSlug)
    {
        var normalized = TenantSlugSuggestions.NormalizeSlug(tenantSlug);
        if (string.Equals(normalized, SystemSlug, StringComparison.OrdinalIgnoreCase))
            return SystemSlug;
        return normalized;
    }

    private static string GenerateRandomString(int length)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        Span<char> buffer = stackalloc char[length];
        Span<byte> randomBytes = stackalloc byte[length];

        RandomNumberGenerator.Fill(randomBytes);
        for (var i = 0; i < length; i++)
            buffer[i] = RandomAlphabet[randomBytes[i] % RandomAlphabet.Length];

        var generated = new string(buffer);
        if (length == RandomSuffixLength && !IsValidRandomPart(generated))
            throw new InvalidOperationException(InvalidRandomPartMessage);

        return generated;
    }
}
