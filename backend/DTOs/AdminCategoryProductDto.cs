namespace KasseAPI_Final.DTOs;

/// <summary>
/// Flat response DTO for admin category products endpoint.
/// Prevents recursive navigation serialization cycles.
/// </summary>
public class AdminCategoryProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int TaxType { get; set; }
    public decimal TaxRate { get; set; }
    public string? ImageUrl { get; set; }
    public int StockQuantity { get; set; }
    public int MinStockLevel { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}
