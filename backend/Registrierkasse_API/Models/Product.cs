using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse.Models
{
    [Table("products")]
    public class Product : BaseEntity
    {
        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [Column("price")]
        public decimal Price { get; set; }

        [Required]
        [Column("tax_type")]
        public TaxType TaxType { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("barcode")]
        [MaxLength(50)]
        public string Barcode { get; set; }

        [Column("category")]
        [MaxLength(50)]
        public string Category { get; set; }

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Column("stock_quantity")]
        public int StockQuantity { get; set; }

        [Column("min_stock_level")]
        public int MinStockLevel { get; set; }

        [Column("unit")]
        [MaxLength(20)]
        public string Unit { get; set; }

        // Navigation properties
        public virtual Inventory Inventory { get; set; }
        public virtual ICollection<OrderItem> OrderItems { get; set; }
        public virtual ICollection<InvoiceItem> InvoiceItems { get; set; }

        // BaseEntity'den miras alınan özellikler:
        // - Id (Guid)
        // - CreatedAt (DateTime)
        // - UpdatedAt (DateTime?)
        // - IsActive (bool)
    }
} 