using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// RKSV uyumlu ürün modeli - Avusturya kasa sistemi standartlarına uygun
    /// </summary>
    [Table("products")]
    public class Product : BaseEntity, ITenantEntity
    {
        /// <summary>FK to <see cref="Models.Tenant"/>; aligns with <see cref="CategoryId"/> composite FK to <see cref="Category"/>.</summary>
        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column("name_de")]
        [MaxLength(200)]
        public string? NameDe { get; set; }

        [Column("name_en")]
        [MaxLength(200)]
        public string? NameEn { get; set; }

        [Column("name_tr")]
        [MaxLength(200)]
        public string? NameTr { get; set; }

        [Required]
        [Column("price")]
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        public decimal Price { get; set; }

        [Required]
        [Column("tax_type")]
        public int TaxType { get; set; } = 1; // 1: Standard, 2: Reduced, etc.

        [Column("description")]
        [MaxLength(2000)]
        public string? Description { get; set; }

        [Column("description_de")]
        [MaxLength(2000)]
        public string? DescriptionDe { get; set; }

        [Column("description_en")]
        [MaxLength(2000)]
        public string? DescriptionEn { get; set; }

        [Column("description_tr")]
        [MaxLength(2000)]
        public string? DescriptionTr { get; set; }

        [Required]
        [Column("category")]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [Column("image_url")]
        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        [Required]
        [Column("stock_quantity")]
        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative")]
        public int StockQuantity { get; set; }

        [Required]
        [Column("min_stock_level")]
        [Range(0, int.MaxValue, ErrorMessage = "Minimum stock level cannot be negative")]
        public int MinStockLevel { get; set; }

        [Column("max_stock_level")]
        [Range(0, int.MaxValue, ErrorMessage = "Maximum stock level cannot be negative")]
        public int? MaxStockLevel { get; set; }

        [Required]
        [Column("unit")]
        [MaxLength(20)]
        public string Unit { get; set; } = string.Empty;

        [Column("cost")]
        [Range(0, double.MaxValue, ErrorMessage = "Cost cannot be negative")]
        public decimal Cost { get; set; }

        [Column("tax_rate")]
        [Range(0, 100, ErrorMessage = "Tax rate must be between 0 and 100")]
        public decimal TaxRate { get; set; }

        [Required]
        [Column("barcode")]
        [MaxLength(50)]
        public string Barcode { get; set; } = string.Empty;

        [Column("category_id")]
        public Guid CategoryId { get; set; }

        /// <summary>Kategori adı (gösterim için; Category navigation'dan senkron tutulur).</summary>
        public virtual Category? CategoryNavigation { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant? Tenant { get; set; }

        // RKSV Compliance Fields
        [Column("is_fiscal_compliant")]
        public bool IsFiscalCompliant { get; set; } = true;

        [Column("fiscal_category_code")]
        [MaxLength(10)]
        public string? FiscalCategoryCode { get; set; } // Avusturya vergi kategorisi kodu

        [Column("is_taxable")]
        public bool IsTaxable { get; set; } = true;

        [Column("tax_exemption_reason")]
        [MaxLength(100)]
        public string? TaxExemptionReason { get; set; } // Vergi muafiyeti nedeni

        [Column("rksv_product_type")]
        [MaxLength(50)]
        public string RksvProductType { get; set; } = "Standard"; // Standard, Reduced, Special, Exempt

        /// <summary>Faz 1: Sellable add-on (Zusatzprodukt); sepette/ödeme/fişte ayrı line item; fiyat burada; stok düşülmez.</summary>
        [Column("is_sellable_addon")]
        public bool IsSellableAddOn { get; set; }

        // Navigation properties
        // public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        // public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
        /// <summary>Bu ürüne atanmış modifier grupları (Extra Zutaten).</summary>
        public virtual ICollection<ProductModifierGroupAssignment> ModifierGroupAssignments { get; set; } = new List<ProductModifierGroupAssignment>();
    }

    /// <summary>
    /// RKSV uyumlu vergi tipleri - Avusturya standartları.
    /// ZeroRate: 0% VAT (Österreich 2026 Reform). Exempt deprecated, use ZeroRate.
    /// </summary>
    public static class TaxTypes
    {
        public const int Standard = 1;    // %20
        public const int Reduced = 2;     // %10 (gıda, kitap, vb.)
        public const int Special = 3;     // %13 (konaklama, vb.)
        public const int ZeroRate = 4;    // %0 (Österreich 2026 - 0% MwSt., nicht Exempt)

        public static readonly int[] All = { Standard, Reduced, Special, ZeroRate };

        public static decimal GetTaxRate(int taxType)
        {
            return taxType switch
            {
                Standard => 20.0m,
                Reduced => 10.0m,
                Special => 13.0m,
                ZeroRate => 0.0m,
                _ => 20.0m
            };
        }

        public static bool IsValidTaxType(int taxType)
        {
            return All.Contains(taxType);
        }
    }

    /// <summary>
    /// RKSV ürün tipleri - Avusturya kasa sistemi standartları
    /// </summary>
    public static class RksvProductTypes
    {
        public const string Standard = "Standard";        // Standart ürün
        public const string Reduced = "Reduced";          // İndirimli vergi oranı
        public const string Special = "Special";          // Özel vergi oranı
        public const string Exempt = "Exempt";            // Vergi muaf
        public const string Service = "Service";          // Hizmet
        public const string Digital = "Digital";          // Dijital ürün

        public static readonly string[] All = { Standard, Reduced, Special, Exempt, Service, Digital };

        public static bool IsValidRksvType(string rksvType)
        {
            return All.Contains(rksvType);
        }
    }
}
