using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Canonical failover type labels persisted on <see cref="TseFailoverLog"/>.</summary>
public static class TseFailoverTypes
{
    public const string Automatic = "Automatic";
    public const string Manual = "Manual";
    public const string Forced = "Forced";

    public static bool IsValid(string? value) =>
        value is Automatic or Manual or Forced;
}

/// <summary>Canonical trigger reason labels persisted on <see cref="TseFailoverLog"/>.</summary>
public static class TseFailoverTriggerReasons
{
    public const string HealthCheckFailed = "HealthCheckFailed";
    public const string Expired = "Expired";
    public const string ManualOverride = "ManualOverride";
    public const string Revoked = "Revoked";
    public const string Forced = "Forced";

    public static bool IsValid(string? value) =>
        value is HealthCheckFailed or Expired or ManualOverride or Revoked or Forced;
}

/// <summary>
/// Audit trail for TSE primary → backup failover events (automatic or manual).
/// </summary>
[Table("tse_failover_logs")]
public class TseFailoverLog : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [Column("primary_device_id")]
    public Guid PrimaryDeviceId { get; set; }

    [Column("backup_device_id")]
    public Guid? BackupDeviceId { get; set; }

    /// <summary><see cref="TseFailoverTypes"/> value.</summary>
    [Required]
    [MaxLength(32)]
    [Column("failover_type")]
    public string FailoverType { get; set; } = TseFailoverTypes.Automatic;

    /// <summary><see cref="TseFailoverTriggerReasons"/> value.</summary>
    [Required]
    [MaxLength(64)]
    [Column("trigger_reason")]
    public string TriggerReason { get; set; } = string.Empty;

    [MaxLength(64)]
    [Column("previous_status")]
    public string? PreviousStatus { get; set; }

    [MaxLength(64)]
    [Column("new_status")]
    public string? NewStatus { get; set; }

    [Column("is_successful")]
    public bool IsSuccessful { get; set; }

    [MaxLength(2000)]
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Required]
    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>AspNetUsers Id (string Identity key). Null for fully automatic system failovers.</summary>
    [MaxLength(450)]
    [Column("performed_by")]
    public string? PerformedBy { get; set; }

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [ForeignKey(nameof(PrimaryDeviceId))]
    public virtual TseDevice? PrimaryDevice { get; set; }

    [ForeignKey(nameof(BackupDeviceId))]
    public virtual TseDevice? BackupDevice { get; set; }

    [ForeignKey(nameof(PerformedBy))]
    public virtual ApplicationUser? PerformedByUser { get; set; }
}
