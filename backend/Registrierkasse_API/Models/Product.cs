using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
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
        [TaxTypeValidation]
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

        public decimal Cost { get; set; }
        public decimal TaxRate { get; set; }
        public string? CategoryId { get; set; }

        // Navigation properties
        public virtual Inventory Inventory { get; set; } // Bire-bir ilişki için uygun
        public virtual ICollection<OrderItem> OrderItems { get; set; }
        public virtual ICollection<InvoiceItem> InvoiceItems { get; set; }

        // BaseEntity'den miras alınan özellikler:
        // - Id (Guid)
        // - CreatedAt (DateTime)
        // - UpdatedAt (DateTime?)
        // - IsActive (bool)
    }

    // TaxType enum validasyonu için attribute ekle
    public class TaxTypeValidationAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
        {
            if (value is TaxType taxType)
            {
                if (taxType != TaxType.Standard && taxType != TaxType.Reduced && taxType != TaxType.Special)
                {
                    return new ValidationResult("TaxType sadece 'standard', 'reduced', 'special' olabilir.");
                }
            }
            return ValidationResult.Success;
        }
    }
} 
