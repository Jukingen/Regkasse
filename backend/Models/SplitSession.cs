using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// POS bill-split workflow snapshot for an active cart. Items are grouped by seat/customer before separate payments.
/// </summary>
[Table("split_sessions")]
public class SplitSession : BaseEntity, ITenantEntity
{
    /// <summary>FK to <see cref="Cart.Id"/> (uuid row id; operational cart key remains <see cref="Cart.CartId"/> string).</summary>
    [Required]
    [Column("original_cart_id")]
    public Guid OriginalCartId { get; set; }

    [ForeignKey(nameof(OriginalCartId))]
    public virtual Cart? OriginalCart { get; set; }

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("cashier_id")]
    public string CashierId { get; set; } = string.Empty;

    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    public virtual ICollection<SplitItem> SplitItems { get; set; } = new List<SplitItem>();
}

/// <summary>One payable portion of a <see cref="SplitSession"/> (seat / guest).</summary>
[Table("split_items")]
public class SplitItem : BaseEntity
{
    [Required]
    [Column("split_session_id")]
    public Guid SplitSessionId { get; set; }

    [ForeignKey(nameof(SplitSessionId))]
    public virtual SplitSession SplitSession { get; set; } = null!;

    [Required]
    [Column("product_id")]
    public Guid ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public virtual Product? Product { get; set; }

    /// <summary>Optional link to the source cart line when split from an open cart.</summary>
    [Column("source_cart_item_id")]
    public Guid? SourceCartItemId { get; set; }

    [Required]
    [Column("quantity")]
    public int Quantity { get; set; }

    [Required]
    [Column("price", TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("customer_name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [Column("seat_number")]
    public int SeatNumber { get; set; }
}
