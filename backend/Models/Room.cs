using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

[Table("rooms")]
public sealed class Room : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("number")]
    public string Number { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("capacity")]
    [Range(1, 20)]
    public int Capacity { get; set; } = 1;

    [Column("status")]
    public RoomStatus Status { get; set; } = RoomStatus.Available;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<GuestFolio> Folios { get; set; } = new List<GuestFolio>();
}
