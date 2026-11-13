using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// Code-seeded OSS destination STANDARD rates. Not a database table.
/// See <c>docs/EINVOICING_EU.md</c>.
/// </summary>
public sealed class OssVatRateRegistry : IOssVatRateRegistry
{
    private static readonly Dictionary<string, string> PrefixAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["EL"] = "GR",
        };

    private static readonly IReadOnlyDictionary<string, OssVatRate> StandardByCountry =
        OssVatRates.All
            .Where(row => row.Code == OssVatRateCodes.Standard)
            .ToDictionary(row => row.CountryCode, StringComparer.OrdinalIgnoreCase);

    public decimal? GetStandardRate(string destinationCountryCode)
    {
        if (string.IsNullOrWhiteSpace(destinationCountryCode))
            return null;

        var code = destinationCountryCode.Trim();
        if (PrefixAliases.TryGetValue(code, out var aliased))
            code = aliased;

        return StandardByCountry.TryGetValue(code, out var row) ? row.Rate : null;
    }

    public IReadOnlyList<OssVatRate> GetAll() => OssVatRates.All;
}
