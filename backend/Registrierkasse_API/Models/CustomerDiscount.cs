using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    [Table("customer_discounts")]
    public class CustomerDiscount : BaseEntity
    {
        [Required]
        [Column("customer_id")]
        public Guid CustomerId { get; set; }

        [Required]
        [Column("discount_type")]
        public DiscountType DiscountType { get; set; }

        [Required]
        [Column("discount_value")]
        public decimal DiscountValue { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("valid_from")]
        public DateTime ValidFrom { get; set; }

        [Column("valid_until")]
        public DateTime? ValidUntil { get; set; }

        [Column("usage_limit")]
        public int UsageLimit { get; set; } = 0; // 0 = unlimited

        [Column("used_count")]
        public int UsedCount { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("product_category_restriction")]
        public string ProductCategoryRestriction { get; set; }

        [Column("minimum_amount")]
        public decimal MinimumAmount { get; set; } = 0;

        [Column("created_by")]
        public string CreatedBy { get; set; }

        // Navigation properties
        public virtual Customer Customer { get; set; }
    }
} 