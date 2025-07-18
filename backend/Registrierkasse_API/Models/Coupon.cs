using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    [Table("coupons")]
    public class Coupon : BaseEntity
    {
        [Required]
        [Column("code")]
        [MaxLength(20)]
        public string Code { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Required]
        [Column("discount_type")]
        public DiscountType DiscountType { get; set; }

        [Required]
        [Column("discount_value")]
        public decimal DiscountValue { get; set; }

        [Column("minimum_amount")]
        public decimal MinimumAmount { get; set; } = 0;

        [Column("maximum_discount")]
        public decimal MaximumDiscount { get; set; } = 0;

        [Column("valid_from")]
        public DateTime ValidFrom { get; set; }

        [Column("valid_until")]
        public DateTime ValidUntil { get; set; }

        [Column("usage_limit")]
        public int UsageLimit { get; set; } = 0; // 0 = unlimited

        [Column("used_count")]
        public int UsedCount { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("is_single_use")]
        public bool IsSingleUse { get; set; } = false;

        [Column("customer_category_restriction")]
        public CustomerCategory? CustomerCategoryRestriction { get; set; }

        [Column("product_category_restriction")]
        public string ProductCategoryRestriction { get; set; }

        [Column("created_by")]
        public string CreatedBy { get; set; }

        // Navigation properties
        public virtual ICollection<CouponUsage> CouponUsages { get; set; }
    }

    public enum DiscountType
    {
        Percentage = 0,
        FixedAmount = 1,
        BuyOneGetOne = 2,
        FreeShipping = 3
    }
} 