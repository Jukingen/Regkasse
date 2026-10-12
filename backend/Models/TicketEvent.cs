using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

public enum TicketSeatStatus
{
    Available = 0,
    Reserved = 1,
    Sold = 2,
}

[Table("ticket_events")]
public sealed class TicketEvent : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("start_at_utc")]
    public DateTime StartAtUtc { get; set; }

    [Column("end_at_utc")]
    public DateTime EndAtUtc { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("venue")]
    public string Venue { get; set; } = string.Empty;

    [Column("capacity")]
    public int Capacity { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<TicketSeat> Seats { get; set; } = new List<TicketSeat>();
}

[Table("ticket_seats")]
public sealed class TicketSeat
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("event_id")]
    public Guid EventId { get; set; }

    public TicketEvent? Event { get; set; }

    [Required]
    [MaxLength(16)]
    [Column("row")]
    public string Row { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    [Column("number")]
    public string Number { get; set; } = string.Empty;

    [MaxLength(64)]
    [Column("section")]
    public string? Section { get; set; }

    [Column("status")]
    public TicketSeatStatus Status { get; set; } = TicketSeatStatus.Available;

    [Column("reserved_until_utc")]
    public DateTime? ReservedUntilUtc { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
