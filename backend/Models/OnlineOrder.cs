using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Customer-facing online order from website / PWA / native app.
/// Completely separate from POS: no RKSV/TSE signing, no fiscal receipts, no payment_details rows.
/// Fulfillment is status-only (pending → accepted → preparing → ready → completed).
/// Optional <see cref="PosCartId"/> is a legacy bridge for staff who push into POS — not required for Manager online-order management.
/// </summary>
[Table("online_orders")]
public class OnlineOrder : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    /// <summary>Tenant-scoped human number, e.g. ORD-001.</summary>
    [Required]
    [MaxLength(32)]
    [Column("order_number")]
    public string OrderNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("customer_name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    [Column("customer_phone")]
    public string CustomerPhone { get; set; } = string.Empty;

    [MaxLength(256)]
    [Column("customer_email")]
    public string? CustomerEmail { get; set; }

    /// <summary>Optional mobile push token (FCM/APNs). No provider wired yet.</summary>
    [MaxLength(512)]
    [Column("customer_device_token")]
    public string? CustomerDeviceToken { get; set; }

    /// <summary><see cref="OnlineOrderTypes"/>.</summary>
    [Required]
    [MaxLength(20)]
    [Column("order_type")]
    public string OrderType { get; set; } = OnlineOrderTypes.Takeaway;

    [MaxLength(40)]
    [Column("table_number")]
    public string? TableNumber { get; set; }

    [MaxLength(500)]
    [Column("delivery_address")]
    public string? DeliveryAddress { get; set; }

    public virtual ICollection<OnlineOrderItem> Items { get; set; } = new List<OnlineOrderItem>();

    [Column("subtotal", TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column("tax", TypeName = "decimal(18,2)")]
    public decimal Tax { get; set; }

    [Column("total", TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    /// <summary><see cref="OnlineOrderPaymentMethods"/>.</summary>
    [Required]
    [MaxLength(20)]
    [Column("payment_method")]
    public string PaymentMethod { get; set; } = OnlineOrderPaymentMethods.Cash;

    /// <summary><see cref="OnlineOrderPaymentStatuses"/>.</summary>
    [Required]
    [MaxLength(20)]
    [Column("payment_status")]
    public string PaymentStatus { get; set; } = OnlineOrderPaymentStatuses.Pending;

    /// <summary><see cref="OnlineOrderStatuses"/>.</summary>
    [Required]
    [MaxLength(20)]
    [Column("order_status")]
    public string OrderStatus { get; set; } = OnlineOrderStatuses.Pending;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("accepted_at")]
    public DateTime? AcceptedAt { get; set; }

    [Column("ready_at")]
    public DateTime? ReadyAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }

    /// <summary><see cref="OnlineOrderSources"/> — web / pwa / native.</summary>
    [Required]
    [MaxLength(20)]
    [Column("source")]
    public string Source { get; set; } = OnlineOrderSources.Web;

    /// <summary>
    /// Optional legacy link after <c>PushOrderToPosAsync</c> (string cart key).
    /// Manager online-order management does not set or require this — status updates alone fulfill the order.
    /// </summary>
    [MaxLength(50)]
    [Column("pos_cart_id")]
    public string? PosCartId { get; set; }

    /// <summary>When the optional POS cart bridge ran. Unused by status-only Manager flow.</summary>
    [Column("pushed_to_pos_at")]
    public DateTime? PushedToPosAt { get; set; }

    /// <summary>Stripe (or mock gateway) PaymentIntent id for online checkout.</summary>
    [MaxLength(128)]
    [Column("stripe_payment_intent_id")]
    public string? StripePaymentIntentId { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    /// <summary>Optional link to CRM customer for loyalty earn.</summary>
    [Column("customer_id")]
    public Guid? CustomerId { get; set; }

    public virtual ICollection<OnlineOrderStatusChange> StatusChanges { get; set; } =
        new List<OnlineOrderStatusChange>();
}

/// <summary>Line item on an <see cref="OnlineOrder"/> (avoids clash with POS <see cref="OrderItem"/>).</summary>
[Table("online_order_items")]
public class OnlineOrderItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("online_order_id")]
    public Guid OnlineOrderId { get; set; }

    [ForeignKey(nameof(OnlineOrderId))]
    public virtual OnlineOrder OnlineOrder { get; set; } = null!;

    /// <summary>Catalog product id at order time (no EF nav — avoids shadow properties).</summary>
    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [Column("quantity")]
    public int Quantity { get; set; }

    /// <summary>Unit price snapshot (EUR) excluding modifier extras unless folded into price.</summary>
    [Column("price", TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column("total", TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    public virtual ICollection<OnlineOrderItemModifier> Modifiers { get; set; } =
        new List<OnlineOrderItemModifier>();
}

/// <summary>Optional add-on / modifier snapshot on an online order line.</summary>
[Table("online_order_item_modifiers")]
public class OnlineOrderItemModifier
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("online_order_item_id")]
    public Guid OnlineOrderItemId { get; set; }

    [ForeignKey(nameof(OnlineOrderItemId))]
    public virtual OnlineOrderItem OnlineOrderItem { get; set; } = null!;

    /// <summary>Optional catalog modifier id when known.</summary>
    [Column("modifier_id")]
    public Guid? ModifierId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("price", TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; } = 1;
}

public static class OnlineOrderTypes
{
    public const string DineIn = "dine-in";
    public const string Takeaway = "takeaway";
    public const string Delivery = "delivery";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        DineIn,
        Takeaway,
        Delivery,
    };
}

public static class OnlineOrderPaymentMethods
{
    public const string Cash = "cash";
    public const string Card = "card";
    public const string Online = "online";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Cash,
        Card,
        Online,
    };
}

public static class OnlineOrderPaymentStatuses
{
    public const string Pending = "pending";
    public const string Paid = "paid";
    public const string Failed = "failed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Pending,
        Paid,
        Failed,
    };
}

public static class OnlineOrderStatuses
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Preparing = "preparing";
    public const string Ready = "ready";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Pending,
        Accepted,
        Preparing,
        Ready,
        Completed,
        Cancelled,
    };
}

public static class OnlineOrderSources
{
    public const string Web = "web";
    public const string Pwa = "pwa";
    public const string Native = "native";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Web,
        Pwa,
        Native,
    };
}
