using System.Text.RegularExpressions;

namespace KasseAPI_Final.Models.Countries;

/// <summary>
/// Immutable, code-seeded description of a country's fiscal and invoicing behavior.
/// Seeds live in <c>CountryProfileRegistry</c> — never in appsettings, and never per tenant.
///
/// A profile deliberately carries **no VAT rates**. Rates belong with tax types and
/// <see cref="VatRegime"/>; see <c>docs/COUNTRIES.md</c> §13.
/// </summary>
public sealed class CountryProfile
{
    private readonly Lazy<Regex> _vatIdRegex;

    public CountryProfile(
        string code,
        string name,
        bool isTenantSelectable,
        string currency,
        string defaultLocale,
        string defaultTimeZone,
        FiscalSystem fiscalSystem,
        IReadOnlyList<EInvoicingStandard> eInvoicingStandards,
        string vatIdPattern,
        IReadOnlyList<VatRegime> allowedVatRegimes)
    {
        Code = code;
        Name = name;
        IsTenantSelectable = isTenantSelectable;
        Currency = currency;
        DefaultLocale = defaultLocale;
        DefaultTimeZone = defaultTimeZone;
        FiscalSystem = fiscalSystem;
        EInvoicingStandards = eInvoicingStandards;
        VatIdPattern = vatIdPattern;
        AllowedVatRegimes = allowedVatRegimes;

        _vatIdRegex = new Lazy<Regex>(
            () => new Regex(vatIdPattern, RegexOptions.CultureInvariant | RegexOptions.Compiled),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>ISO 3166-1 alpha-2 for real countries, or the <c>EU_DEFAULT</c> sentinel.</summary>
    public string Code { get; }

    /// <summary>English display name. Admin UI should translate via i18n rather than print this directly.</summary>
    public string Name { get; }

    /// <summary>False for registry-only fallbacks that must never appear in the create-tenant country list.</summary>
    public bool IsTenantSelectable { get; }

    /// <summary>ISO 4217 currency copied into <c>company_settings.Currency</c> at provisioning.</summary>
    public string Currency { get; }

    /// <summary>BCP-47 locale copied into <c>company_settings.Language</c> at provisioning.</summary>
    public string DefaultLocale { get; }

    /// <summary>IANA time zone copied into <c>company_settings.TimeZone</c> at provisioning.</summary>
    public string DefaultTimeZone { get; }

    /// <summary>Which cash-register fiscal module applies. Only <see cref="Countries.FiscalSystem.RKSV_AT"/> is implemented.</summary>
    public FiscalSystem FiscalSystem { get; }

    /// <summary>Standards this country may use once the corresponding builder and flag exist. May be empty.</summary>
    public IReadOnlyList<EInvoicingStandard> EInvoicingStandards { get; }

    /// <summary>Single source of truth for the country's VAT-ID shape. Do not copy this into controllers or forms.</summary>
    public string VatIdPattern { get; }

    /// <summary>Compiled form of <see cref="VatIdPattern"/>.</summary>
    public Regex VatIdRegex => _vatIdRegex.Value;

    /// <summary>Regimes that may be assigned to a tenant in this country. Never empty.</summary>
    public IReadOnlyList<VatRegime> AllowedVatRegimes { get; }

    /// <summary>True when <paramref name="regime"/> is valid for this country.</summary>
    public bool Supports(VatRegime regime) => AllowedVatRegimes.Contains(regime);

    /// <summary>
    /// Shape-only VAT-ID check. Does not contact VIES and does not assert the number is registered.
    /// **Strict:** the input is matched as given — no trimming and no case folding — so this stays
    /// byte-identical to the checks already live on the fiscal path. Call sites that accept
    /// user-typed input normalize before calling.
    /// </summary>
    public bool MatchesVatIdShape(string? vatId) =>
        !string.IsNullOrEmpty(vatId) && VatIdRegex.IsMatch(vatId);

    public override string ToString() => $"{Code} ({FiscalSystem})";
}

/// <summary>Well-known <see cref="CountryProfile.Code"/> values.</summary>
public static class CountryProfileCodes
{
    public const string Austria = "AT";
    public const string Germany = "DE";
    public const string Switzerland = "CH";

    /// <summary>
    /// Registry-only fallback. Not an ISO 3166-1 alpha-2 code and never tenant-selectable.
    /// </summary>
    public const string EuDefault = "EU_DEFAULT";
}
