namespace KasseAPI_Final.Models.DTOs;

public sealed class DemoImportRequest
{
    public bool OverwriteExisting { get; set; }

    /// <summary>When empty, all categories (except excluded) are imported.</summary>
    public List<string> SelectedCategories { get; set; } = [];

    /// <summary>Category names to skip even when selected.</summary>
    public List<string> ExcludedCategories { get; set; } = [];

    /// <summary>When non-empty, only these demo catalog product ids are imported (within category filter).</summary>
    public List<Guid> SelectedProductIds { get; set; } = [];

    /// <summary>None | IncreasePercent | DecreasePercent | RoundUpToIncrement</summary>
    public string? PriceAdjustmentMode { get; set; }

    /// <summary>Percent for increase/decrease modes (e.g. 10 = +10%).</summary>
    public decimal? PriceAdjustmentPercent { get; set; }

    /// <summary>Increment for RoundUpToIncrement (e.g. 0.50).</summary>
    public decimal? PriceRoundIncrement { get; set; }

    /// <summary>none | categoryPlaceholder | defaultAsset. Empty defaults to categoryPlaceholder.</summary>
    public string? ImageMode { get; set; }

    /// <summary>Optional per-product price/tax overrides from import wizard.</summary>
    public List<DemoImportProductOverrideDto> ProductOverrides { get; set; } = [];
}

public sealed record DemoImportCatalogCategoryDto(
    string Name,
    string? Description,
    int SortOrder,
    int ProductCount,
    decimal VatRate);

public sealed record DemoImportCatalogProductDto(
    Guid Id,
    string Name,
    string Category,
    decimal Price,
    decimal TaxRate);

public sealed record DemoImportCatalogDto(
    IReadOnlyList<DemoImportCatalogCategoryDto> Categories,
    IReadOnlyList<DemoImportCatalogProductDto> Products);
