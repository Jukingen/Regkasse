using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

public enum KitchenOrderStatus
{
    Pending = 0,
    InPreparation = 1,
    Ready = 2,
    Served = 3,
    Cancelled = 4,
}

public enum KitchenOrderItemStatus
{
    Pending = 0,
    Preparing = 1,
    Ready = 2,
    Served = 3,
}

[Table("kitchen_orders")]
public sealed class KitchenOrder : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Column("cart_id")]
    public Guid? CartId { get; set; }

    public Cart? Cart { get; set; }

    [MaxLength(16)]
    [Column("table_number")]
    public string? TableNumber { get; set; }

    [Column("cash_register_id")]
    public Guid CashRegisterId { get; set; }

    public CashRegister? CashRegister { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("created_by_user_id")]
    public string CreatedByUserId { get; set; } = string.Empty;

    [Column("status")]
    public KitchenOrderStatus Status { get; set; } = KitchenOrderStatus.Pending;

    [Column("priority")]
    public int Priority { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("ready_at_utc")]
    public DateTime? ReadyAtUtc { get; set; }

    [Column("served_at_utc")]
    public DateTime? ServedAtUtc { get; set; }

    public ICollection<KitchenOrderItem> Items { get; set; } = new List<KitchenOrderItem>();
}

[Table("kitchen_order_items")]
public sealed class KitchenOrderItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("kitchen_order_id")]
    public Guid KitchenOrderId { get; set; }

    public KitchenOrder? KitchenOrder { get; set; }

    [Column("product_id")]
    public Guid? ProductId { get; set; }

    public Product? Product { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("status")]
    public KitchenOrderItemStatus Status { get; set; } = KitchenOrderItemStatus.Pending;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
