using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries.Strategies.EuDefault;

/// <summary>
/// Skeleton for the registry-only <c>EU_DEFAULT</c> profile. Generic EU VAT handling (reverse charge /
/// OSS) is not implemented — see <c>docs/EINVOICING_EU.md</c>.
/// </summary>
public sealed class EuDefaultTaxStrategy : PlannedCountryTaxStrategy
{
    public EuDefaultTaxStrategy()
        : base(CountryProfileCodes.EuDefault, CountryStrategyDocs.EuDefault)
    {
    }
}

/// <summary>
/// Skeleton for the registry-only <c>EU_DEFAULT</c> profile. EN 16931 invoice building is not
/// implemented, and this strategy will never submit to a tax authority or Peppol network —
/// see <c>docs/EINVOICING_EU.md</c>.
/// </summary>
public sealed class EuDefaultInvoiceStrategy : PlannedCountryInvoiceStrategy
{
    public EuDefaultInvoiceStrategy()
        : base(CountryProfileCodes.EuDefault, CountryStrategyDocs.EuDefault)
    {
    }
}
