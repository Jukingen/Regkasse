namespace KasseAPI_Final.Models.DTOs;

/// <summary>Lightweight projection for admin product list queries.</summary>
public class ProductListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameDe { get; set; }
    public string? NameEn { get; set; }
    public string? NameTr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionDe { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionTr { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public int MinStockLevel { get; set; }
    public int? MaxStockLevel { get; set; }
    public int TaxType { get; set; }
    public decimal TaxRate { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public bool IsActive { get; set; }
    public bool IsTaxable { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string Unit { get; set; } = "pcs";
    public decimal Cost { get; set; }
    public string? ImageUrl { get; set; }
}

public class ProductCategoryFilterOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ProductAvailableFiltersDto
{
    public List<int> TaxTypes { get; set; } = new();
    public List<ProductCategoryFilterOptionDto> Categories { get; set; } = new();
}
