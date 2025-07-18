using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registrierkasse_API.Models
{
    public class Order : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string OrderNumber { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public decimal TotalAmount { get; set; }

        [MaxLength(10)]
        public string? TableNumber { get; set; }

        [MaxLength(50)]
        public string? WaiterName { get; set; }

        [MaxLength(200)]
        public string? Notes { get; set; }

        public string? CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }

        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
