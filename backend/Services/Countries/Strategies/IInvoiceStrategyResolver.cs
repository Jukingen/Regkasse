using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries.Strategies;

/// <summary>
/// Selects the <see cref="IInvoiceStrategy"/> for a country profile and VAT regime. Fails closed with
/// <see cref="UnknownTaxRegimeException"/>, same as <see cref="ITaxStrategyResolver"/>.
/// </summary>
public interface IInvoiceStrategyResolver
{
    /// <exception cref="UnknownTaxRegimeException">
    /// The regime is not allowed for the profile, or no strategy is registered for the country.
    /// </exception>
    IInvoiceStrategy Resolve(CountryProfile profile, VatRegime vatRegime);
}
