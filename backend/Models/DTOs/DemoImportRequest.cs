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
}

public sealed record DemoImportCatalogCategoryDto(
    string Name,
    string? Description,
    int SortOrder,
    int ProductCount);

public sealed record DemoImportCatalogProductDto(
    Guid Id,
    string Name,
    string Category,
    decimal Price,
    decimal TaxRate);

public sealed record DemoImportCatalogDto(
    IReadOnlyList<DemoImportCatalogCategoryDto> Categories,
    IReadOnlyList<DemoImportCatalogProductDto> Products);
