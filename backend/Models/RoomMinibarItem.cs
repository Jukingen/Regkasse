using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

public enum RoomMinibarItemStatus
{
    Pending,
    Charged,
    Voided,
}

/// <summary>
/// Pending minibar consumption for a lodging room. Charging it adds a non-fiscal folio line.
/// </summary>
[Table("room_minibar_items")]
public sealed class RoomMinibarItem : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Column("room_id")]
    public Guid RoomId { get; set; }

    public Room? Room { get; set; }

    [Column("product_id")]
    public Guid? ProductId { get; set; }

    public Product? Product { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("unit_price", TypeName = "decimal(10,2)")]
    public decimal UnitPrice { get; set; }

    [Column("status")]
    public RoomMinibarItemStatus Status { get; set; } = RoomMinibarItemStatus.Pending;

    [Column("charged_folio_id")]
    public Guid? ChargedFolioId { get; set; }

    public GuestFolio? ChargedFolio { get; set; }

    [Column("charged_at_utc")]
    public DateTime? ChargedAtUtc { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
