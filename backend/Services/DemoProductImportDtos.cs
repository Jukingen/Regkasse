namespace KasseAPI_Final.Services;

public sealed class DemoData
{
    public List<DemoCategory> Categories { get; set; } = [];
    public List<DemoProduct> Products { get; set; } = [];
}

public sealed class DemoCategory
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public decimal VatRate { get; set; } = 10m;
}

public sealed class DemoProduct
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal TaxRate { get; set; } = 10m;
    public string Category { get; set; } = string.Empty;
}

public sealed class CategoryImportSummary
{
    public string CategoryName { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public int Created { get; set; }
    public int Skipped { get; set; }
}

public sealed class ImportResult
{
    public bool Success { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int SelectedCategoryCount { get; set; }
    public int TotalProductCount { get; set; }
    /// <summary>New categories inserted during this import (existing rows are not counted).</summary>
    public int CategoriesCreated { get; set; }
    /// <summary>Products created or updated in this import (excludes skipped).</summary>
    public int ImportedProductCount { get; set; }
    public decimal AverageImportedPrice { get; set; }
    public List<CategoryImportSummary> CategorySummaries { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<Guid> CategoryIds { get; set; } = [];
    public IReadOnlyList<Guid> ProductIds { get; set; } = [];
}
