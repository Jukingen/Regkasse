using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

/// <summary>
/// Flat DTO for admin product API responses. No navigation properties to avoid JSON cycles.
/// Used by GET/POST/PUT admin product endpoints.
/// </summary>
public class AdminProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string Category { get; set; } = string.Empty;
    public int TaxType { get; set; }
    public decimal TaxRate { get; set; }
    public bool IsActive { get; set; }
    public string Unit { get; set; } = "pcs";
    public int StockQuantity { get; set; }
    public int MinStockLevel { get; set; }
    public decimal Cost { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsFiscalCompliant { get; set; }
    public bool IsTaxable { get; set; }
    public string? FiscalCategoryCode { get; set; }
    public string? TaxExemptionReason { get; set; }
    public string RksvProductType { get; set; } = "Standard";

    /// <summary>
    /// Map Product entity to flat DTO (no CategoryNavigation, no ModifierGroupAssignments).
    /// </summary>
    public static AdminProductDto FromProduct(Product p)
    {
        return new AdminProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price,
            Description = p.Description,
            Barcode = p.Barcode,
            CategoryId = p.CategoryId,
            Category = p.Category ?? string.Empty,
            TaxType = p.TaxType,
            TaxRate = p.TaxRate,
            IsActive = p.IsActive,
            Unit = p.Unit ?? "pcs",
            StockQuantity = p.StockQuantity,
            MinStockLevel = p.MinStockLevel,
            Cost = p.Cost,
            ImageUrl = p.ImageUrl,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            CreatedBy = p.CreatedBy,
            UpdatedBy = p.UpdatedBy,
            IsFiscalCompliant = p.IsFiscalCompliant,
            IsTaxable = p.IsTaxable,
            FiscalCategoryCode = p.FiscalCategoryCode,
            TaxExemptionReason = p.TaxExemptionReason,
            RksvProductType = p.RksvProductType ?? "Standard"
        };
    }
}
