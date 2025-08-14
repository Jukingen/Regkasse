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
