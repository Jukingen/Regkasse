using System.Text.RegularExpressions;

namespace KasseAPI_Final.Models;

/// <summary>
/// Shape validation and normalization for ISO 3166-1 alpha-2 country codes.
/// Checks the two-letter form only; it does not assert the code is an assigned country.
/// </summary>
public static partial class Iso3166CountryCode
{
    /// <summary>Allows empty so a caller can clear an optional country field.</summary>
    public const string OptionalPattern = "^([A-Za-z]{2})?$";

    public const string ValidationMessage = "Must be a 2-letter ISO 3166-1 alpha-2 country code.";

    [GeneratedRegex("^[A-Za-z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && CodeRegex().IsMatch(value.Trim());

    /// <summary>
    /// Trims and upper-cases a country code. Null, empty, or whitespace normalizes to <c>null</c>
    /// (field cleared) and still returns <c>true</c>; a malformed non-empty value returns <c>false</c>.
    /// </summary>
    public static bool TryNormalize(string? value, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = null;
            return true;
        }

        var trimmed = value.Trim();
        if (!CodeRegex().IsMatch(trimmed))
        {
            normalized = null;
            return false;
        }

        normalized = trimmed.ToUpperInvariant();
        return true;
    }
}
