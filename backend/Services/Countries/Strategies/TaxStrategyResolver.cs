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

        // OSS destination rates are Paket 30-d; AT must not silently use AustriaTaxStrategy.
        if (string.Equals(profile.Code, CountryProfileCodes.Austria, StringComparison.OrdinalIgnoreCase)
            && vatRegime == VatRegime.EU_OSS)
        {
            throw new ArgumentException("AT tenant + EU_OSS is not supported");
        }

        // Reverse charge is a VAT regime, not a fiscal system — route to EU_DEFAULT regardless of country.
        if (vatRegime == VatRegime.EU_REVERSE_CHARGE)
        {
            if (!_byCountryCode.TryGetValue(CountryProfileCodes.EuDefault, out var euStrategy))
                throw new UnknownTaxRegimeException(profile.Code, vatRegime);

            return euStrategy;
        }

        if (!_byCountryCode.TryGetValue(profile.Code, out var strategy))
            throw new UnknownTaxRegimeException(profile.Code, vatRegime);

        return strategy;
    }
}
