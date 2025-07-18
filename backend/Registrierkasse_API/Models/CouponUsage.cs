using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    [Table("coupon_usages")]
    public class CouponUsage : BaseEntity
    {
        [Required]
        [Column("coupon_id")]
        public Guid CouponId { get; set; }

        [Column("customer_id")]
        public Guid? CustomerId { get; set; }

        [Column("invoice_id")]
        public Guid? InvoiceId { get; set; }

        [Column("order_id")]
        public Guid? OrderId { get; set; }

        [Required]
        [Column("discount_amount")]
        public decimal DiscountAmount { get; set; }

        [Column("used_at")]
        public DateTime UsedAt { get; set; } = DateTime.UtcNow;

        [Column("used_by")]
        public string UsedBy { get; set; }

        [Column("session_id")]
        public string SessionId { get; set; }

        // Navigation properties
        public virtual Coupon Coupon { get; set; }
        public virtual Customer Customer { get; set; }
        public virtual Invoice Invoice { get; set; }
        public virtual Order Order { get; set; }
    }
} 