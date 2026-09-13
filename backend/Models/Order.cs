using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("orders")]
    public class Order : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string OrderId { get; set; } = string.Empty;

        public int? TableNumber { get; set; }

        [MaxLength(100)]
        public string? WaiterName { get; set; }

        [MaxLength(100)]
        public string? CustomerName { get; set; }

        [MaxLength(20)]
        public string? CustomerPhone { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [Required]
        public DateTime OrderDate { get; set; }

        [Required]
        public OrderStatus Status { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public Guid? CustomerId { get; set; }

        /// <summary>Sprint 6: Optional idempotency key for order creation; retries with same key return existing order.</summary>
        [MaxLength(64)]
        [Column("idempotency_key")]
        public string? IdempotencyKey { get; set; }

        /// <summary>Ambient tenant for POS pre-orders. Legacy kitchen rows may be null.</summary>
        [Column("tenant_id")]
        public Guid? TenantId { get; set; }

        /// <summary>True when this row tracks a paid POS layaway / Vorbestellung (fiscal receipt already issued).</summary>
        [Column("is_preorder")]
        public bool IsPreorder { get; set; }

        /// <summary><see cref="PreorderStatuses"/> — pending / ready / collected / cancelled.</summary>
        [MaxLength(20)]
        [Column("preorder_status")]
        public string? PreorderStatus { get; set; }

        /// <summary>Besorgerzettel number, e.g. BS2609201. Not a fiscal Belegnummer.</summary>
        [MaxLength(20)]
        [Column("preorder_number")]
        public string? PreorderNumber { get; set; }

        [Column("preorder_paid_amount", TypeName = "decimal(18,2)")]
        public decimal PreorderPaidAmount { get; set; }

        [Column("preorder_remaining_amount", TypeName = "decimal(18,2)")]
        public decimal PreorderRemainingAmount { get; set; }

        [Column("preorder_pickup_deadline")]
        public DateTime? PreorderPickupDeadline { get; set; }

        [Column("preorder_pickup_weeks")]
        public int PreorderPickupWeeks { get; set; }

        [Column("preorder_ready_at")]
        public DateTime? PreorderReadyAt { get; set; }

        [Column("preorder_collected_at")]
        public DateTime? PreorderCollectedAt { get; set; }

        [Column("preorder_customer_notes", TypeName = "text")]
        public string? PreorderCustomerNotes { get; set; }

        /// <summary>Fiscal sale payment that created this pre-order. Pickup never creates a second payment.</summary>
        [Column("source_payment_id")]
        public Guid? SourcePaymentId { get; set; }

        /// <summary>Latest fiscal payment on this pre-order (create or remaining-balance sale).</summary>
        [Column("last_preorder_payment_id")]
        public Guid? LastPreorderPaymentId { get; set; }

        /// <summary>Belegnummer snapshot for cashier search (same as payment_details.ReceiptNumber).</summary>
        [MaxLength(256)]
        [Column("receipt_number")]
        public string? ReceiptNumber { get; set; }

        // Navigation properties
        public virtual Customer? Customer { get; set; }
        public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }

    public enum OrderStatus
    {
        Pending = 1,
        Confirmed = 2,
        InProgress = 3,
        Ready = 4,
        Delivered = 5,
        Cancelled = 6,
        Completed = 7
    }
}
