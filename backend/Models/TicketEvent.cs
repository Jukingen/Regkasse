using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

public enum TicketSeatStatus
{
    Available = 0,
    Reserved = 1,
    Sold = 2,
}

public enum TicketSeatRole
{
    Standard = 0,
    Accessible = 1,
    Companion = 2,
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

    /// <summary>Stage position for the seat grid. <c>top</c> or <c>bottom</c>.</summary>
    [Required]
    [MaxLength(16)]
    [Column("stage_edge")]
    public string StageEdge { get; set; } = "top";

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

    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    public TicketPriceCategory? Category { get; set; }

    [Column("grid_column")]
    public int? GridColumn { get; set; }

    [Column("seat_role")]
    public TicketSeatRole SeatRole { get; set; } = TicketSeatRole.Standard;

    /// <summary>
    /// Accessible seat this companion seat accompanies. One-way; only meaningful when
    /// <see cref="SeatRole"/> is <see cref="TicketSeatRole.Companion"/>.
    /// </summary>
    [Column("companion_of_seat_id")]
    public Guid? CompanionOfSeatId { get; set; }

    public TicketSeat? CompanionOfSeat { get; set; }

    [MaxLength(450)]
    [Column("reserved_by_user_id")]
    public string? ReservedByUserId { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Tenant-level ticket price catalog (VIP, standard, student).</summary>
[Table("ticket_price_categories")]
public sealed class TicketPriceCategory : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Per-event price and capacity for one catalog category.
/// Not <see cref="ITenantEntity"/>: the parent <see cref="TicketEvent"/> carries the tenant filter,
/// same as <see cref="TicketSeat"/>.
/// </summary>
[Table("ticket_event_category_prices")]
public sealed class TicketEventCategoryPrice
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("event_id")]
    public Guid EventId { get; set; }

    public TicketEvent? Event { get; set; }

    [Column("category_id")]
    public Guid CategoryId { get; set; }

    public TicketPriceCategory? Category { get; set; }

    [Column("price", TypeName = "numeric(18,2)")]
    public decimal Price { get; set; }

    [Column("capacity")]
    public int Capacity { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
