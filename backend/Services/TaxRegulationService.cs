using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Austrian MwSt regulation catalog and tenant product impact estimates.
/// Rates are curated (not loaded from FinanzOnline); keep in sync with <see cref="TaxGroupSeedData"/>.
/// </summary>
public sealed class TaxRegulationService : ITaxRegulationService
{
    private static readonly TaxRegulation[] Catalog =
    [
        new TaxRegulation
        {
            EffectiveDate = new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            StandardRate = 20m,
            ReducedRate = 10m,
            ReducedNewRate = 10m,
            MiddleRate = 13m,
            ZeroRate = 0m,
            IsActive = false,
            Description =
                "Austrian MwSt before dedicated 4.9% reduced band (Normalsatz 20%, Ermäßigt 10%, Mittel 13%, Null 0%).",
            AllowedRates = [0m, 10m, 13m, 20m],
        },
        new TaxRegulation
        {
            EffectiveDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            StandardRate = 20m,
            ReducedRate = 10m,
            ReducedNewRate = 4.9m,
            MiddleRate = 13m,
            ZeroRate = 0m,
            IsActive = true,
            Description =
                "Current Austrian MwSt bands used by Regkasse: 20% standard, 13% middle, 10% reduced, 4.9% reduced (new), 0% zero.",
            AllowedRates = [0m, 4.9m, 10m, 13m, 20m],
        },
    ];

    private readonly AppDbContext _db;
    private readonly ILogger<TaxRegulationService> _logger;

    public TaxRegulationService(AppDbContext db, ILogger<TaxRegulationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<TaxRegulation> GetCurrentRegulationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = Catalog.Where(r => r.IsActive).OrderByDescending(r => r.EffectiveDate).FirstOrDefault()
                      ?? Catalog.OrderByDescending(r => r.EffectiveDate).First();
        return Task.FromResult(Clone(current));
    }

    public Task<IEnumerable<TaxRegulation>> GetRegulationHistoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<TaxRegulation> history = Catalog
            .OrderByDescending(r => r.EffectiveDate)
            .Select(Clone)
            .ToArray();
        return Task.FromResult(history);
    }

    public async Task<bool> IsTaxRateValidAsync(decimal rate, CancellationToken cancellationToken = default)
    {
        var regulation = await GetCurrentRegulationAsync(cancellationToken).ConfigureAwait(false);
        var normalized = decimal.Round(rate, 2, MidpointRounding.AwayFromZero);
        return GetDistinctAllowedRates(regulation).Contains(normalized);
    }

    public async Task<TaxChangeImpact> GetTaxChangeImpactAsync(
        Guid tenantId,
        decimal oldRate,
        decimal newRate,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        var oldNormalized = decimal.Round(oldRate, 2, MidpointRounding.AwayFromZero);
        var newNormalized = decimal.Round(newRate, 2, MidpointRounding.AwayFromZero);

        // Ambient tenant filter applies; TenantId predicate keeps Super Admin impact scoped to requested tenant.
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.TaxRate == oldNormalized)
            .Select(p => p.Price)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var catalogValue = products.Sum();
        var vatDelta = catalogValue * ((newNormalized - oldNormalized) / 100m);

        _logger.LogInformation(
            "Tax change impact for tenant {TenantId}: {Count} products from {OldRate}% to {NewRate}% (catalog={Catalog}, vatDelta={VatDelta})",
            tenantId,
            products.Count,
            oldNormalized,
            newNormalized,
            catalogValue,
            vatDelta);

        return new TaxChangeImpact
        {
            TenantId = tenantId,
            OldRate = oldNormalized,
            NewRate = newNormalized,
            AffectedProductCount = products.Count,
            AffectedCatalogValue = decimal.Round(catalogValue, 2, MidpointRounding.AwayFromZero),
            EstimatedVatDelta = decimal.Round(vatDelta, 2, MidpointRounding.AwayFromZero),
        };
    }

    internal static HashSet<decimal> GetDistinctAllowedRates(TaxRegulation regulation)
    {
        IEnumerable<decimal> source = regulation.AllowedRates is { Count: > 0 }
            ? regulation.AllowedRates
            :
            [
                regulation.ZeroRate,
                regulation.ReducedNewRate,
                regulation.ReducedRate,
                regulation.MiddleRate,
                regulation.StandardRate,
            ];

        return source
            .Select(r => decimal.Round(r, 2, MidpointRounding.AwayFromZero))
            .ToHashSet();
    }

    private static TaxRegulation Clone(TaxRegulation source) => new()
    {
        EffectiveDate = source.EffectiveDate,
        StandardRate = source.StandardRate,
        ReducedRate = source.ReducedRate,
        ReducedNewRate = source.ReducedNewRate,
        MiddleRate = source.MiddleRate,
        ZeroRate = source.ZeroRate,
        IsActive = source.IsActive,
        Description = source.Description,
        AllowedRates = GetDistinctAllowedRates(source).OrderBy(r => r).ToArray(),
    };
}
