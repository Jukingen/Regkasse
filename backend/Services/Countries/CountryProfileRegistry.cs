using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Code-seeded country profiles.
///
/// **Verification status:** the Austrian seed mirrors the values already live in production
/// (<c>CompanySettingsController.CreateSettingsShell</c> and the Austrian UID pattern used across the
/// fiscal path), so it is authoritative. The DE, CH, and EU_DEFAULT seeds describe **shape only** and
/// have not been checked against official sources yet — they gate nothing today because no DE/CH/EU
/// module exists. Source verification is a separate, reviewed change; do not treat these values as
/// legal guidance. See <c>docs/COUNTRIES.md</c>.
/// </summary>
public sealed class CountryProfileRegistry : ICountryProfileRegistry
{
    private static readonly CountryProfile AustriaProfile = new(
        code: CountryProfileCodes.Austria,
        name: "Austria",
        isTenantSelectable: true,
        // Must stay identical to the live company-settings defaults, otherwise provisioning drifts.
        currency: "EUR",
        defaultLocale: "de-DE",
        defaultTimeZone: "Europe/Vienna",
        fiscalSystem: FiscalSystem.RKSV_AT,
        // RKSV receipts are not an e-invoicing standard; AT declares none until EN 16931 is wired.
        eInvoicingStandards: [],
        // Identical to the pattern already enforced on the fiscal path (UID: ATU + 8 digits).
        vatIdPattern: @"^ATU\d{8}$",
        allowedVatRegimes:
        [
            VatRegime.AT_RKSV_STANDARD,
            VatRegime.EU_REVERSE_CHARGE,
            VatRegime.EU_OSS,
            VatRegime.NON_EU,
        ]);

    private static readonly CountryProfile GermanyProfile = new(
        code: CountryProfileCodes.Germany,
        name: "Germany",
        isTenantSelectable: true,
        currency: "EUR",
        defaultLocale: "de-DE",
        defaultTimeZone: "Europe/Berlin",
        fiscalSystem: FiscalSystem.KASSENSICHERHEIT_DE,
        eInvoicingStandards: [EInvoicingStandard.ZUGFERD, EInvoicingStandard.XRECHNUNG],
        vatIdPattern: @"^DE\d{9}$",
        allowedVatRegimes:
        [
            VatRegime.DE_USTG_STANDARD,
            VatRegime.DE_KLEINUNTERNEHMER,
            VatRegime.EU_REVERSE_CHARGE,
            VatRegime.EU_OSS,
            VatRegime.NON_EU,
        ]);

    private static readonly CountryProfile SwitzerlandProfile = new(
        code: CountryProfileCodes.Switzerland,
        name: "Switzerland",
        isTenantSelectable: true,
        currency: "CHF",
        defaultLocale: "de-CH",
        defaultTimeZone: "Europe/Zurich",
        fiscalSystem: FiscalSystem.MWST_CH,
        eInvoicingStandards: [EInvoicingStandard.QR_RECHNUNG],
        // CHE-123.456.789 with an optional language-specific VAT suffix.
        vatIdPattern: @"^CHE-\d{3}\.\d{3}\.\d{3}( (MWST|TVA|IVA))?$",
        // Switzerland is outside the EU VAT area: no reverse charge, no OSS.
        allowedVatRegimes:
        [
            VatRegime.CH_MWST_STANDARD,
            VatRegime.CH_KLEINUNTERNEHMER,
            VatRegime.NON_EU,
        ]);

    private static readonly CountryProfile EuDefaultProfile = new(
        code: CountryProfileCodes.EuDefault,
        name: "European Union (default profile)",
        // Registry-only fallback; must never reach the create-tenant country list.
        isTenantSelectable: false,
        currency: "EUR",
        // Never copied onto a tenant, because the profile is not selectable.
        defaultLocale: "en",
        defaultTimeZone: "UTC",
        fiscalSystem: FiscalSystem.NONE,
        eInvoicingStandards: [EInvoicingStandard.EN_16931],
        // Broad EU VAT-ID shape; per-country patterns live in their own profiles.
        vatIdPattern: @"^[A-Z]{2}[A-Za-z0-9+*.]{2,12}$",
        allowedVatRegimes:
        [
            VatRegime.EU_REVERSE_CHARGE,
            VatRegime.EU_OSS,
            VatRegime.NON_EU,
        ]);

    private static readonly IReadOnlyList<CountryProfile> Seeds =
    [
        AustriaProfile,
        GermanyProfile,
        SwitzerlandProfile,
        EuDefaultProfile,
    ];

    private static readonly IReadOnlyDictionary<string, CountryProfile> ByCode =
        Seeds.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<CountryProfile> Selectable =
        [.. Seeds.Where(p => p.IsTenantSelectable)];

    public IReadOnlyList<CountryProfile> All => Seeds;

    public IReadOnlyList<CountryProfile> TenantSelectable => Selectable;

    public CountryProfile Default => AustriaProfile;

    public bool TryGet(string? code, out CountryProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(code) && ByCode.TryGetValue(code.Trim(), out var found))
        {
            profile = found;
            return true;
        }

        profile = AustriaProfile;
        return false;
    }

    public CountryProfile GetOrDefault(string? code) =>
        TryGet(code, out var profile) ? profile : AustriaProfile;

    public CountryProfile Get(string? code) =>
        TryGet(code, out var profile) ? profile : throw new UnknownCountryCodeException(code);
}
