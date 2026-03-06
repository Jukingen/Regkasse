namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// Katalog kategori satırı. JSON: camelCase.
    /// </summary>
    public class CatalogCategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal VatRate { get; set; }
    }

    /// <summary>
    /// Katalog ürün satırı; ürün alanları + ürüne atanmış modifier grupları. EF entity dönülmez.
    /// </summary>
    public class CatalogProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public int StockQuantity { get; set; }
        public int? MinStockLevel { get; set; }
        public string? Unit { get; set; }
        public string? ProductCategory { get; set; }
        public Guid? CategoryId { get; set; }
        public int TaxType { get; set; }
        public decimal TaxRate { get; set; }
        public bool IsActive { get; set; }
        public bool IsFiscalCompliant { get; set; }
        public string? FiscalCategoryCode { get; set; }
        public bool IsTaxable { get; set; }
        public string? TaxExemptionReason { get; set; }
        public string RksvProductType { get; set; } = "Standard";
        public decimal? Cost { get; set; }
        /// <summary>Ürüne atanmış modifier grupları (Extra Zutaten).</summary>
        public List<ModifierGroupDto> ModifierGroups { get; set; } = new();
    }

    /// <summary>
    /// GET /Product/catalog yanıtı. JSON: camelCase.
    /// </summary>
    public class CatalogResponseDto
    {
        public List<CatalogCategoryDto> Categories { get; set; } = new();
        public List<CatalogProductDto> Products { get; set; } = new();
    }
}
