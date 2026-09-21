namespace KasseAPI_Final.Models.Countries;

/// <summary>
/// One STANDARD OSS destination VAT rate. In-code seed only — not an EF entity and not a
/// <see cref="CountryProfile"/> field. See <c>docs/EINVOICING_EU.md</c>.
/// </summary>
public sealed record OssVatRate(
    string CountryCode,
    string Code,
    decimal Rate,
    DateOnly EffectiveFrom);

/// <summary>Well-known <see cref="OssVatRate.Code"/> values.</summary>
public static class OssVatRateCodes
{
    public const string Standard = "STANDARD";
}

/// <summary>
/// OSS destination STANDARD rates. Greek VAT-ID prefix <c>EL</c> is not a row;
/// <c>OssVatRateRegistry</c> aliases it to <c>GR</c>.
/// </summary>
public static class OssVatRates
{
    private static readonly DateOnly StandardFrom = new(2024, 1, 1);
    private static readonly DateOnly FinlandFrom = new(2024, 9, 1);

    public static IReadOnlyList<OssVatRate> All { get; } =
    [
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("AT", 20m, StandardFrom),
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("DE", 19m, StandardFrom),
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("FR", 20m, StandardFrom),
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("IT", 22m, StandardFrom),
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("NL", 21m, StandardFrom),
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("ES", 21m, StandardFrom),
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("PL", 23m, StandardFrom),
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("BE", 21m, StandardFrom),
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("IE", 23m, StandardFrom),
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("PT", 23m, StandardFrom),
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("SE", 25m, StandardFrom),
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("DK", 25m, StandardFrom),
        // Source: EU VAT rates (FI 25.5 effective 2024-09-01)
        Rate("FI", 25.5m, FinlandFrom),
        // Source: EU VAT rates — see docs/EINVOICING_EU.md
        Rate("GR", 24m, StandardFrom),
    ];

    private static OssVatRate Rate(string countryCode, decimal rate, DateOnly effectiveFrom) =>
        new(countryCode, OssVatRateCodes.Standard, rate, effectiveFrom);
}
