using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries.Strategies.Switzerland;

/// <summary>
/// Skeleton. Swiss MWST behavior is not implemented — see <c>docs/FISCAL_SWITZERLAND.md</c>.
/// </summary>
public sealed class SwitzerlandTaxStrategy : PlannedCountryTaxStrategy
{
    public SwitzerlandTaxStrategy()
        : base(CountryProfileCodes.Switzerland, CountryStrategyDocs.Switzerland)
    {
    }
}

/// <summary>
/// Skeleton. Swiss invoicing (QR-Rechnung) is not implemented — see <c>docs/FISCAL_SWITZERLAND.md</c>.
/// </summary>
public sealed class SwitzerlandInvoiceStrategy : PlannedCountryInvoiceStrategy
{
    public SwitzerlandInvoiceStrategy()
        : base(CountryProfileCodes.Switzerland, CountryStrategyDocs.Switzerland)
    {
    }
}
