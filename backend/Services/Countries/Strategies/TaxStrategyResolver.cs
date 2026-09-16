using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries.Strategies;

/// <inheritdoc cref="ITaxStrategyResolver" />
public sealed class TaxStrategyResolver : ITaxStrategyResolver
{
    private readonly IReadOnlyDictionary<string, ITaxStrategy> _byCountryCode;

    public TaxStrategyResolver(IEnumerable<ITaxStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        // Duplicate registrations are a wiring bug, not a runtime condition — let ToDictionary throw.
        _byCountryCode = strategies.ToDictionary(s => s.CountryCode, StringComparer.OrdinalIgnoreCase);
    }

    public ITaxStrategy Resolve(CountryProfile profile, VatRegime vatRegime)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.Supports(vatRegime))
            throw new UnknownTaxRegimeException(profile.Code, vatRegime);

        if (!_byCountryCode.TryGetValue(profile.Code, out var strategy))
            throw new UnknownTaxRegimeException(profile.Code, vatRegime);

        return strategy;
    }
}
