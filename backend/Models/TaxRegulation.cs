namespace KasseAPI_Final.Models;

/// <summary>
/// Snapshot of Austrian MwSt rates for a regulation period (not a persisted entity).
/// </summary>
public class TaxRegulation
{
    public DateTime EffectiveDate { get; set; }

    public decimal StandardRate { get; set; } = 20m;

    public decimal ReducedRate { get; set; } = 10m;

    /// <summary>4.9% reduced rate (e.g. certain digital publications / goods).</summary>
    public decimal ReducedNewRate { get; set; } = 4.9m;

    public decimal MiddleRate { get; set; } = 13m;

    public decimal ZeroRate { get; set; } = 0m;

    public bool IsActive { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Canonical allowed VAT percent rates for this regulation.
    /// When empty, derived from the named rate fields (distinct).
    /// </summary>
    public IReadOnlyList<decimal> AllowedRates { get; set; } = [];
}

/// <summary>
/// Estimated impact of changing products from one VAT rate to another within a tenant catalog.
/// </summary>
public class TaxChangeImpact
{
    public Guid TenantId { get; set; }

    public decimal OldRate { get; set; }

    public decimal NewRate { get; set; }

    public int AffectedProductCount { get; set; }

    /// <summary>Sum of product unit prices currently taxed at <see cref="OldRate"/>.</summary>
    public decimal AffectedCatalogValue { get; set; }

    /// <summary>
    /// Approximate VAT delta on catalog value: (newRate - oldRate) / 100 * AffectedCatalogValue.
    /// Informational only — not a fiscal forecast.
    /// </summary>
    public decimal EstimatedVatDelta { get; set; }
}
