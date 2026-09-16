using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries.Strategies;

/// <inheritdoc cref="IInvoiceStrategyResolver" />
public sealed class InvoiceStrategyResolver : IInvoiceStrategyResolver
{
    private readonly IReadOnlyDictionary<string, IInvoiceStrategy> _byCountryCode;

    public InvoiceStrategyResolver(IEnumerable<IInvoiceStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        _byCountryCode = strategies.ToDictionary(s => s.CountryCode, StringComparer.OrdinalIgnoreCase);
    }

    public IInvoiceStrategy Resolve(CountryProfile profile, VatRegime vatRegime)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.Supports(vatRegime))
            throw new UnknownTaxRegimeException(profile.Code, vatRegime);

        if (!_byCountryCode.TryGetValue(profile.Code, out var strategy))
            throw new UnknownTaxRegimeException(profile.Code, vatRegime);

        return strategy;
    }
}
