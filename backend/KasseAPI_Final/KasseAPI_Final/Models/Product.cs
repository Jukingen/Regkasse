using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("products")]
    public class Product : BaseEntity
    {
        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("price")]
        public decimal Price { get; set; }

        [Required]
        [Column("tax_type")]
        public TaxType TaxType { get; set; }

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("barcode")]
        [MaxLength(50)]
        public string Barcode { get; set; } = string.Empty;

        [Column("category")]
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty;

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Column("stock_quantity")]
        public int StockQuantity { get; set; }

        [Column("min_stock_level")]
        public int MinStockLevel { get; set; }

        [Column("unit")]
        [MaxLength(20)]
        public string Unit { get; set; } = string.Empty;

        public decimal Cost { get; set; }
        public decimal TaxRate { get; set; }
        public string? CategoryId { get; set; }

        // Navigation properties - şimdilik basit tutuyoruz
        // public virtual Inventory Inventory { get; set; } = null!;
        // public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        // public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
        // public virtual ICollection<ProductVariation> Variations { get; set; } = new List<ProductVariation>();
        // public virtual ICollection<ProductOption> Options { get; set; } = new List<ProductOption>();
    }

    public enum TaxType
    {
        Standard = 20,
        Reduced = 10,
        Special = 13
    }
}
