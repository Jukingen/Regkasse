using System.Text.RegularExpressions;

namespace KasseAPI_Final.Models.Countries;

/// <summary>
/// Single source for VAT-ID shape patterns. Needed as compile-time constants because
/// <c>[RegularExpression]</c> attributes cannot read the registry at runtime; the
/// <see cref="CountryProfile"/> seeds consume the same constants, so there is exactly one literal per
/// country in the repository (see <c>AGENTS.md</c> § Country &amp; Fiscal Regimes).
///
/// Matching is deliberately **strict**: no trimming and no case folding. Call sites that accept
/// user-typed input normalize first — that normalization is a property of the call site, not of the
/// pattern, and must not be moved in here.
/// </summary>
public static class VatIdPatterns
{
    /// <summary>Austrian UID: <c>ATU</c> + 8 digits.</summary>
    public const string Austria = @"^ATU\d{8}$";

    /// <summary>German USt-IdNr.: <c>DE</c> + 9 digits.</summary>
    public const string Germany = @"^DE\d{9}$";

    /// <summary>Swiss UID: <c>CHE-123.456.789</c> with an optional language-specific VAT suffix.</summary>
    public const string Switzerland = @"^CHE-\d{3}\.\d{3}\.\d{3}( (MWST|TVA|IVA))?$";

    /// <summary>
    /// Registry-only <c>EU_DEFAULT</c> VAT-ID shape: ISO country prefix + 8–12 alphanumeric.
    /// VIES formats remain per member state; punctuation (legacy IE <c>+</c>, Swiss <c>CHE-…</c>)
    /// and lowercase are rejected. Not an allowlist.
    /// </summary>
    public const string EuDefault = @"^[A-Z]{2}[A-Z0-9]{8,12}$";

    /// <summary>Compiled Austrian matcher shared by every runtime call site.</summary>
    public static readonly Regex AustriaRegex =
        new(Austria, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Strict Austrian UID check: no trimming, case-sensitive. Null / empty is invalid.</summary>
    public static bool IsAustrianUid(string? vatId) =>
        !string.IsNullOrEmpty(vatId) && AustriaRegex.IsMatch(vatId);
}
