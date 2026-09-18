using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Code-seeded country profiles.
///
/// **Verification status:** the Austrian seed mirrors the values already live in production
/// (<c>CompanySettingsController.CreateSettingsShell</c> and the Austrian UID pattern used across the
/// fiscal path), so it is authoritative. Seeded field values were checked against official sources in
/// Paket 13; <c>// Source:</c> citations live on the seeds below. See <c>docs/COUNTRIES.md</c> §14
/// Seed Sources. Do not treat these values as legal guidance. Profiles feed strategy resolvers,
/// country feature-flag defaults, Super Admin provisioning (`GET /api/admin/countries`), and
/// <c>IVatIdValidator</c>. DE/CH/EU fiscal modules remain shape-only and are gated by feature flags.
/// </summary>
public sealed class CountryProfileRegistry : ICountryProfileRegistry
{
    private static readonly CountryProfile AustriaProfile = new(
        // Source: ISO 3166-1 alpha-2 AT
        code: CountryProfileCodes.Austria,
        name: "Austria",
        isTenantSelectable: true,
        // Must stay identical to the live company-settings defaults, otherwise provisioning drifts.
        // Source: ISO 4217 EUR
        currency: "EUR",
        // Source: production default (CreateSettingsShell). BCP-47 AT is de-AT; kept de-DE — Paket 13-b decision (A).
        defaultLocale: "de-DE",
        // Source: IANA Time Zone Database Europe/Vienna
        defaultTimeZone: "Europe/Vienna",
        // Source: RKSV, BGBl. II Nr. 410/2015; FinanzOnline
        fiscalSystem: FiscalSystem.RKSV_AT,
        // RKSV receipts are not an e-invoicing standard; AT declares none until EN 16931 is wired.
        // Source: RKSV Belege are cash-register receipts, not EN 16931
        eInvoicingStandards: [],
        // Identical to the pattern already enforced on the fiscal path (UID: ATU + 8 digits).
        // Source: BMF UID; ATU + 8 digits; FinanzOnline
        vatIdPattern: VatIdPatterns.Austria,
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
        // Source: ISO 4217 EUR
        currency: "EUR",
        // Source: IETF BCP 47 de-DE
        defaultLocale: "de-DE",
        // Source: IANA Time Zone Database Europe/Berlin
        defaultTimeZone: "Europe/Berlin",
        // Source: KassenSichV (Kassensicherungsverordnung)
        fiscalSystem: FiscalSystem.KASSENSICHERHEIT_DE,
        // Source: FeRD ZUGFeRD; KoSIT XRechnung (EN 16931 CIUS)
        eInvoicingStandards: [EInvoicingStandard.ZUGFERD, EInvoicingStandard.XRECHNUNG],
        // Source: UStG USt-IdNr. DE + 9 digits; EU VIES
        vatIdPattern: VatIdPatterns.Germany,
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
        // Source: ISO 4217 CHF
        currency: "CHF",
        // Source: IETF BCP 47 de-CH (German-speaking default)
        defaultLocale: "de-CH",
        // Source: IANA Time Zone Database Europe/Zurich
        defaultTimeZone: "Europe/Zurich",
        // Source: MWSTG; ESTV
        fiscalSystem: FiscalSystem.MWST_CH,
        // Source: SIX Interbank Clearing QR-bill specification
        eInvoicingStandards: [EInvoicingStandard.QR_RECHNUNG],
        // CHE-123.456.789 with an optional language-specific VAT suffix.
        // Source: ESTV / Zefix UID CHE-xxx.xxx.xxx + optional MWST|TVA|IVA
        vatIdPattern: VatIdPatterns.Switzerland,
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
        // Source: registry-only sentinel; not ISO 3166-1 alpha-2
        isTenantSelectable: false,
        // Source: ISO 4217 EUR (euro-area default; never copied onto a tenant)
        currency: "EUR",
        // Never copied onto a tenant, because the profile is not selectable.
        // Source: IETF BCP 47 en (never copied onto a tenant)
        defaultLocale: "en",
        // Source: IANA UTC; no single EU zone (never copied onto a tenant)
        defaultTimeZone: "UTC",
        // Source: no EU-level cash-register fiscalisation
        fiscalSystem: FiscalSystem.NONE,
        // Source: CEN EN 16931-1; ViDA is a timeline, not a builder
        eInvoicingStandards: [EInvoicingStandard.EN_16931],
        // Broad EU VAT-ID shape; per-country patterns live in their own profiles.
        // Source: VIES country-prefixed VAT numbers (country code + 8-12 alphanumeric); rare national exceptions documented in CountryProfile notes.
        vatIdPattern: VatIdPatterns.EuDefault,
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
