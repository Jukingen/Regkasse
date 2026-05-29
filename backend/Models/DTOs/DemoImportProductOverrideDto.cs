namespace KasseAPI_Final.Models.DTOs;

/// <summary>Per-catalog-product overrides applied during demo import (wizard price/tax step).</summary>
public sealed class DemoImportProductOverrideDto
{
    public Guid CatalogProductId { get; set; }

    public decimal? Price { get; set; }

    public decimal? TaxRate { get; set; }
}
