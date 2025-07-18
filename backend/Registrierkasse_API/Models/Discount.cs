using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    [Table("discounts")]
    public class Discount : BaseEntity
    {
        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [Column("discount_type")]
        public string DiscountType { get; set; } // percentage, fixed_amount

        [Required]
        [Column("value")]
        public decimal Value { get; set; }

        [Column("start_date")]
        public DateTime? StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("min_purchase_amount")]
        public decimal? MinPurchaseAmount { get; set; }

        [Column("max_discount_amount")]
        public decimal? MaxDiscountAmount { get; set; }

        [Column("code")]
        [MaxLength(50)]
        public string Code { get; set; }

        // BaseEntity'den miras alınan özellikler:
        // - Id (Guid)
        // - CreatedAt (DateTime)
        // - UpdatedAt (DateTime?)
        // - IsActive (bool)
    }
} 
