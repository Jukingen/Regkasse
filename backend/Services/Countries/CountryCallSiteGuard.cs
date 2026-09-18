using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries.Strategies;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Live payment / invoice / RKSV call sites stay Austria-only until Paket 30-c.
/// Non-AT strategies may return shape results in unit tests without generating AT documents.
/// </summary>
public static class CountryCallSiteGuard
{
    public static void EnsureAustriaWired(CountryProfile profile, string member)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.Equals(profile.Code, CountryProfileCodes.Austria, StringComparison.OrdinalIgnoreCase))
            return;

        var doc = profile.Code switch
        {
            var code when string.Equals(code, CountryProfileCodes.Germany, StringComparison.OrdinalIgnoreCase)
                => CountryStrategyDocs.Germany,
            var code when string.Equals(code, CountryProfileCodes.Switzerland, StringComparison.OrdinalIgnoreCase)
                => CountryStrategyDocs.Switzerland,
            _ => CountryStrategyDocs.EuDefault,
        };

        throw new NotImplementedException(
            $"{profile.Code} invoicing is not wired into the live call site ({member}). See {doc}.");
    }
}
