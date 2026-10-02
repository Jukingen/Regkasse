using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Countries;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Countries;

/// <summary>
/// CH MWST percents for one mandant: code seed, then an optional
/// <c>tenant_settings</c> overlay (<see cref="ChMwstRateSettings.Key"/>).
/// Austria and Germany catalogs are never replaced.
/// </summary>
public interface IChMwstEffectiveRates
{
    /// <summary>
    /// Effective CH rows. <paramref name="applyOverride"/> is false when the mandant is not CH,
    /// so a stray settings row cannot change the seed.
    /// </summary>
    ChMwstRateResolution Resolve(Guid? tenantId, bool applyOverride);

    /// <summary>
    /// Registry view whose CH rows are <see cref="Resolve"/> for <paramref name="tenantId"/>.
    /// Every other country, including Austria, is read from <paramref name="seeds"/>.
    /// </summary>
    ICountryTaxTypeRegistry ForCalculation(ICountryTaxTypeRegistry seeds, Guid tenantId);
}

public sealed record ChMwstRateResolution(string Source, IReadOnlyList<CountryTaxType> Rates);

public static class ChMwstRateSettings
{
    /// <summary>
    /// JSON object of CH <see cref="CountryTaxTypeCodes"/> to percent, e.g.
    /// <c>{"STANDARD":7.7,"REDUCED_1":2.5,"LODGING":3.7}</c>.
    /// </summary>
    public const string Key = "Mwst:ChRates";

    public const string SourceSeed = "seed";

    public const string SourceTenantOverride = "tenant_override";
}

public sealed class ChMwstEffectiveRates : IChMwstEffectiveRates
{
    private static readonly HashSet<string> AllowedCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        CountryTaxTypeCodes.Standard,
        CountryTaxTypeCodes.Reduced1,
        CountryTaxTypeCodes.Lodging,
    };

    private readonly AppDbContext _db;
    private readonly ICountryTaxTypeRegistry _seeds;

    public ChMwstEffectiveRates(AppDbContext db, ICountryTaxTypeRegistry seeds)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _seeds = seeds ?? throw new ArgumentNullException(nameof(seeds));
    }

    public ChMwstRateResolution Resolve(Guid? tenantId, bool applyOverride)
    {
        var seed = _seeds.Get(CountryProfileCodes.Switzerland);
        if (!applyOverride || tenantId is not Guid id || id == Guid.Empty)
            return new ChMwstRateResolution(ChMwstRateSettings.SourceSeed, seed);

        var raw = _db.TenantSettings.AsNoTracking()
            .Where(s => s.TenantId == id && s.Key == ChMwstRateSettings.Key)
            .Select(s => s.Value)
            .FirstOrDefault();

        return TryApply(seed, raw, out var effective)
            ? new ChMwstRateResolution(ChMwstRateSettings.SourceTenantOverride, effective)
            : new ChMwstRateResolution(ChMwstRateSettings.SourceSeed, seed);
    }

    public ICountryTaxTypeRegistry ForCalculation(ICountryTaxTypeRegistry seeds, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        var resolution = Resolve(tenantId, applyOverride: true);
        if (resolution.Source == ChMwstRateSettings.SourceSeed)
            return seeds;

        return new ChOverlayTaxTypeRegistry(seeds, resolution.Rates);
    }

    internal static bool TryApply(
        IReadOnlyList<CountryTaxType> seed,
        string? raw,
        out IReadOnlyList<CountryTaxType> effective)
    {
        effective = seed;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        Dictionary<string, decimal>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<Dictionary<string, decimal>>(raw);
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed is null || parsed.Count == 0)
            return false;

        var replacements = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, rate) in parsed)
        {
            if (string.IsNullOrWhiteSpace(key) || !AllowedCodes.Contains(key.Trim()) || rate < 0m || rate > 100m)
                return false;
            replacements[key.Trim()] = rate;
        }

        effective = seed
            .Select(row => replacements.TryGetValue(row.Code, out var rate)
                ? new CountryTaxType(
                    row.CountryCode,
                    row.Code,
                    rate,
                    row.Label,
                    row.EffectiveFrom,
                    row.EffectiveTo)
                : row)
            .ToArray();
        return true;
    }

    private sealed class ChOverlayTaxTypeRegistry : ICountryTaxTypeRegistry
    {
        private readonly ICountryTaxTypeRegistry _inner;
        private readonly IReadOnlyList<CountryTaxType> _ch;

        public ChOverlayTaxTypeRegistry(ICountryTaxTypeRegistry inner, IReadOnlyList<CountryTaxType> ch)
        {
            _inner = inner;
            _ch = ch;
        }

        public IReadOnlyList<CountryTaxType> All =>
            _inner.All
                .Where(row => !string.Equals(row.CountryCode, CountryProfileCodes.Switzerland, StringComparison.OrdinalIgnoreCase))
                .Concat(_ch)
                .ToArray();

        public IReadOnlyList<CountryTaxType> Get(string? countryCode)
        {
            if (string.Equals(countryCode?.Trim(), CountryProfileCodes.Switzerland, StringComparison.OrdinalIgnoreCase))
                return _ch;
            return _inner.Get(countryCode);
        }
    }
}
