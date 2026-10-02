using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

[Table("appointments")]
public sealed class Appointment : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Column("customer_id")]
    public Guid? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    [Column("service_product_id")]
    public Guid? ServiceProductId { get; set; }

    public Product? ServiceProduct { get; set; }

    [Column("staff_id")]
    [MaxLength(450)]
    public string? StaffId { get; set; }

    [Column("start_utc")]
    public DateTime StartUtc { get; set; }

    [Column("end_utc")]
    public DateTime EndUtc { get; set; }

    [Column("status")]
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("version")]
    public int Version { get; set; } = 1;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("created_by_user_id")]
    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }
}
