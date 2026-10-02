using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

[Table("guest_folios")]
public sealed class GuestFolio : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Column("customer_id")]
    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    [Column("room_id")]
    public Guid RoomId { get; set; }

    public Room? Room { get; set; }

    public ICollection<GuestFolioItem> Items { get; set; } = new List<GuestFolioItem>();

    [Column("check_in")]
    public DateTime CheckIn { get; set; }

    [Column("check_out")]
    public DateTime? CheckOut { get; set; }

    [Column("status")]
    public GuestFolioStatus Status { get; set; } = GuestFolioStatus.Open;

    [Column("balance", TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
