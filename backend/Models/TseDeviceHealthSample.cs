using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Time-series health sample for a <see cref="TseDevice"/> (trend charts / reports).
/// Distinct from process-wide <c>TseHealthAuditLog</c> transition rows.
/// </summary>
[Table("tse_device_health_samples")]
public sealed class TseDeviceHealthSample
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("device_id")]
    public Guid DeviceId { get; set; }

    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    [Required]
    [Column("checked_at_utc")]
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("health_score")]
    public int HealthScore { get; set; }

    [Required]
    [Column("health_status")]
    public TseHealthStatus HealthStatus { get; set; }

    [MaxLength(500)]
    [Column("message")]
    public string? Message { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("is_backup")]
    public bool IsBackup { get; set; }

    /// <summary>Probe wall-clock duration in milliseconds (null for legacy samples).</summary>
    [Column("response_time_ms")]
    public int? ResponseTimeMs { get; set; }

    [ForeignKey(nameof(DeviceId))]
    public TseDevice? Device { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
}
