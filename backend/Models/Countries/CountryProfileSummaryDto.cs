using KasseAPI_Final.Models;

namespace KasseAPI_Final.Models.Countries;

/// <summary>Tenant-selectable country profile for Super Admin provisioning APIs.</summary>
public sealed class CountryProfileSummaryDto
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Currency { get; init; }

    public required string DefaultLocale { get; init; }

    public required FiscalSystem FiscalSystem { get; init; }

    public required IReadOnlyList<EInvoicingStandard> EInvoicingStandards { get; init; }

    public required IReadOnlyList<VatRegime> AllowedVatRegimes { get; init; }

    /// <summary>VAT-ID shape from the country profile seed. Admin UI must not keep a second copy.</summary>
    public required string VatIdPattern { get; init; }

    /// <summary>Operator-facing fiscal system name. Not an ISO country code.</summary>
    public required string FiscalSystemLabel { get; init; }

    public required IReadOnlyList<CountryFiscalSectionDto> FiscalSections { get; init; }

    public static CountryProfileSummaryDto From(CountryProfile profile) => new()
    {
        Code = profile.Code,
        Name = profile.Name,
        Currency = profile.Currency,
        DefaultLocale = profile.DefaultLocale,
        FiscalSystem = profile.FiscalSystem,
        EInvoicingStandards = profile.EInvoicingStandards,
        AllowedVatRegimes = profile.AllowedVatRegimes,
        VatIdPattern = profile.VatIdPattern,
        FiscalSystemLabel = FiscalSystemLabels.For(profile.FiscalSystem),
        FiscalSections = CountryFiscalSectionCatalog.For(profile),
    };
}

public sealed class CountryFiscalSectionDto
{
    public required string Id { get; init; }

    public required string TitleKey { get; init; }

    public required string HelperKey { get; init; }

    public required IReadOnlyList<CountryFiscalFlagDto> Flags { get; init; }
}

public sealed class CountryFiscalFlagDto
{
    public required string Name { get; init; }

    public required bool Enabled { get; init; }

    public required bool Locked { get; init; }
}

/// <summary>
/// Wizard sections derived from the profile. Flag names stay on the server payload.
/// </summary>
public static class CountryFiscalSectionCatalog
{
    public static IReadOnlyList<CountryFiscalSectionDto> For(CountryProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var sections = new List<CountryFiscalSectionDto>();

        if (profile.FiscalSystem == FiscalSystem.RKSV_AT)
        {
            sections.Add(Section(
                "rksv",
                "tenants.create.fiscal.rksv.title",
                "tenants.create.fiscal.rksv.helper",
                Flag("Fiscal.RksvAt", enabled: true, locked: true)));
        }

        if (profile.FiscalSystem == FiscalSystem.KASSENSICHERHEIT_DE)
        {
            sections.Add(Section(
                "kassenSicherheit",
                "tenants.create.fiscal.kassenSicherheit.title",
                "tenants.create.fiscal.kassenSicherheit.helper",
                Flag("Fiscal.KassenSicherheitDe", enabled: true, locked: false)));
        }

        if (profile.EInvoicingStandards.Contains(EInvoicingStandard.QR_RECHNUNG))
        {
            sections.Add(Section(
                "qrRechnung",
                "tenants.create.fiscal.qrRechnung.title",
                "tenants.create.fiscal.qrRechnung.helper",
                Flag("EInvoicing.QrRechnung", enabled: true, locked: false)));
        }

        if (profile.EInvoicingStandards.Contains(EInvoicingStandard.EN_16931))
        {
            sections.Add(Section(
                "en16931",
                "tenants.create.fiscal.en16931.title",
                "tenants.create.fiscal.en16931.helper",
                Flag("EInvoicing.En16931", enabled: true, locked: false)));
        }

        return sections;
    }

    private static CountryFiscalSectionDto Section(
        string id,
        string titleKey,
        string helperKey,
        CountryFiscalFlagDto flag) => new()
    {
        Id = id,
        TitleKey = titleKey,
        HelperKey = helperKey,
        Flags = [flag],
    };

    private static CountryFiscalFlagDto Flag(string name, bool enabled, bool locked) => new()
    {
        Name = name,
        Enabled = enabled,
        Locked = locked,
    };
}

/// <summary>Display names for <see cref="FiscalSystem"/>. Country codes stay on <see cref="CountryProfile.Code"/>.</summary>
public static class FiscalSystemLabels
{
    public static string For(FiscalSystem system) => system switch
    {
        FiscalSystem.RKSV_AT => "RKSV",
        FiscalSystem.KASSENSICHERHEIT_DE => "KassenSicherheit",
        FiscalSystem.MWST_CH => "MWST",
        FiscalSystem.NONE => "None",
        _ => system.ToString(),
    };
}
