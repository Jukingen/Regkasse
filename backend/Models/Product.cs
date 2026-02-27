using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// RKSV uyumlu ürün modeli - Avusturya kasa sistemi standartlarına uygun
    /// </summary>
    [Table("products")]
    public class Product : BaseEntity
    {
        [Required]
        [Column("name")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("price")]
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        public decimal Price { get; set; }

        [Required]
        [Column("tax_type")]
        public int TaxType { get; set; } = 1; // 1: Standard, 2: Reduced, etc.

        [Column("description")]
        public string? Description { get; set; }

        [Required]
        [Column("category")]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Required]
        [Column("stock_quantity")]
        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative")]
        public int StockQuantity { get; set; }

        [Required]
        [Column("min_stock_level")]
        [Range(0, int.MaxValue, ErrorMessage = "Minimum stock level cannot be negative")]
        public int MinStockLevel { get; set; }

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
        public Guid? CategoryId { get; set; }

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

        // Navigation properties
        // public virtual Category CategoryNavigation { get; set; } = null!;
        // public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        // public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
    }

    /// <summary>
    /// RKSV uyumlu vergi tipleri - Avusturya standartları
    /// </summary>
    public static class TaxTypes
    {
        public const int Standard = 1;    // %20
        public const int Reduced = 2;      // %10 (gıda, kitap, vb.)
        public const int Special = 3;      // %13 (konaklama, vb.)
        public const int Exempt = 4;        // %0 (vergisiz)
        
        public static readonly int[] All = { Standard, Reduced, Special, Exempt };
        
        public static decimal GetTaxRate(int taxType)
        {
            return taxType switch
            {
                Standard => 20.0m,
                Reduced => 10.0m,
                Special => 13.0m,
                Exempt => 0.0m,
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
