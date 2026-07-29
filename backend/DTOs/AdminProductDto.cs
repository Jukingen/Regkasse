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
    public string? NameDe { get; set; }
    public string? NameEn { get; set; }
    public string? NameTr { get; set; }
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string? DescriptionDe { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionTr { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string Category { get; set; } = string.Empty;
    public int TaxType { get; set; }
    public decimal TaxRate { get; set; }
    public Guid TaxGroupId { get; set; }
    public ProductTaxGroupSummaryDto? TaxGroup { get; set; }
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
    public int Version { get; set; } = 1;
    public Guid? OriginalProductId { get; set; }
    public DateTime? ArchivedAt { get; set; }

    /// <summary>
    /// Map Product entity to flat DTO (no CategoryNavigation, no ModifierGroupAssignments).
    /// </summary>
    public static AdminProductDto FromProduct(Product p)
    {
        return new AdminProductDto
        {
            Id = p.Id,
            Name = p.Name,
            NameDe = p.NameDe,
            NameEn = p.NameEn,
            NameTr = p.NameTr,
            Price = p.Price,
            Description = p.Description,
            DescriptionDe = p.DescriptionDe,
            DescriptionEn = p.DescriptionEn,
            DescriptionTr = p.DescriptionTr,
            Barcode = p.Barcode,
            CategoryId = p.CategoryId,
            Category = p.Category ?? string.Empty,
            TaxType = p.TaxType,
            TaxRate = p.TaxRate,
            TaxGroupId = p.TaxGroupId,
            TaxGroup = ProductTaxGroupSummaryDto.FromEntity(p.TaxGroup),
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
            RksvProductType = p.RksvProductType ?? "Standard",
            Version = p.Version <= 0 ? 1 : p.Version,
            OriginalProductId = p.OriginalProductId,
            ArchivedAt = p.ArchivedAt,
        };
    }
}

/// <summary>Compact tax group snapshot for product list/detail UI.</summary>
public sealed class ProductTaxGroupSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public string? AustrianCode { get; set; }
    public bool IsDefault { get; set; }
    public bool IsSystem { get; set; }

    public static ProductTaxGroupSummaryDto? FromEntity(TaxGroup? g) =>
        g == null
            ? null
            : new ProductTaxGroupSummaryDto
            {
                Id = g.Id,
                Name = g.Name,
                Rate = g.Rate,
                Color = g.Color,
                Icon = g.Icon,
                AustrianCode = g.AustrianCode,
                IsDefault = g.IsDefault,
                IsSystem = g.IsSystem,
            };
}
