using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries.Strategies.Germany;

/// <summary>
/// Skeleton. German VAT (UStG) behavior is not implemented — see <c>docs/FISCAL_GERMANY.md</c>.
/// </summary>
public sealed class GermanyTaxStrategy : PlannedCountryTaxStrategy
{
    public GermanyTaxStrategy()
        : base(CountryProfileCodes.Germany, CountryStrategyDocs.Germany)
    {
    }
}

/// <summary>
/// Skeleton. German invoicing (KassenSichV / DSFinV-K / ZUGFeRD / XRechnung) is not implemented —
/// see <c>docs/FISCAL_GERMANY.md</c>.
/// </summary>
public sealed class GermanyInvoiceStrategy : PlannedCountryInvoiceStrategy
{
    public GermanyInvoiceStrategy()
        : base(CountryProfileCodes.Germany, CountryStrategyDocs.Germany)
    {
    }
}
