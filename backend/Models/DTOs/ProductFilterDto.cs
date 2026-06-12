using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>Advanced query filters for admin product list.</summary>
public class ProductFilterDto
{
    // Text search
    public string? SearchTerm { get; set; }
    public bool SearchInName { get; set; } = true;
    public bool SearchInDescription { get; set; }
    public bool SearchInBarcode { get; set; }

    // Price range
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    // Stock
    public StockFilterType? StockStatus { get; set; }
    public int? MinStock { get; set; }
    public int? MaxStock { get; set; }

    // Tax
    public List<int> TaxTypes { get; set; } = new();

    // Categories
    public List<Guid> CategoryIds { get; set; } = new();

    // Status — set in AdminProductsController.MergeLegacyListParams from query isActive (true|false|all), not model-bound.
    [BindNever]
    public bool? IsActive { get; set; }
    public bool? IsTaxable { get; set; }

    // Date range
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }

    // Sorting
    public string? SortBy { get; set; } = "Name";
    public string? SortDirection { get; set; } = "asc";

    // Pagination
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public enum StockFilterType
{
    InStock,
    OutOfStock,
    LowStock,
    Overstock,
    All
}

public class ProductListResponse
{
    public List<ProductListDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public ProductAvailableFiltersDto AvailableFilters { get; set; } = new();
    public ProductFilterSummaryDto ActiveFilters { get; set; } = new();
}

public class ProductFilterSummaryDto
{
    public int ActiveFilterCount { get; set; }
    public Dictionary<string, object> AppliedFilters { get; set; } = new();
    public List<int> AvailableTaxTypes { get; set; } = new();
}
