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

    public static CountryProfileSummaryDto From(CountryProfile profile) => new()
    {
        Code = profile.Code,
        Name = profile.Name,
        Currency = profile.Currency,
        DefaultLocale = profile.DefaultLocale,
        FiscalSystem = profile.FiscalSystem,
        EInvoicingStandards = profile.EInvoicingStandards,
        AllowedVatRegimes = profile.AllowedVatRegimes,
    };
}
