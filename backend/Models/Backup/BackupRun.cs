using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Operational metadata for one backup attempt. Not a fiscal document — separate from Tier-0 business tables.
/// </summary>
[Table("backup_runs")]
public sealed class BackupRun : KasseAPI_Final.Models.IRunLeaseColumns
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("status")]
    public BackupRunStatus Status { get; set; } = BackupRunStatus.Queued;

    [Required]
    [Column("trigger_source")]
    public BackupTriggerSource TriggerSource { get; set; }

    /// <summary>Which execution adapter produced artifacts (Fake vs production stack).</summary>
    [Required]
    [MaxLength(64)]
    [Column("adapter_kind")]
    public string AdapterKind { get; set; } = string.Empty;

    /// <summary>Optional client-supplied key; unique when set — idempotent retries.</summary>
    [MaxLength(200)]
    [Column("idempotency_key")]
    public string? IdempotencyKey { get; set; }

    [MaxLength(450)]
    [Column("requested_by_user_id")]
    public string? RequestedByUserId { get; set; }

    [Required]
    [Column("requested_at")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [Column("queued_at")]
    public DateTime? QueuedAt { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [MaxLength(100)]
    [Column("failure_code")]
    public string? FailureCode { get; set; }

    [MaxLength(4000)]
    [Column("failure_detail")]
    public string? FailureDetail { get; set; }

    [MaxLength(100)]
    [Column("correlation_id")]
    public string? CorrelationId { get; set; }

    [Column("lease_expires_at_utc")]
    public DateTime? LeaseExpiresAtUtc { get; set; }

    [Column("last_heartbeat_at_utc")]
    public DateTime? LastHeartbeatAtUtc { get; set; }

    [Column("stale_recovered_at_utc")]
    public DateTime? StaleRecoveredAtUtc { get; set; }

    [MaxLength(500)]
    [Column("stale_recovery_reason")]
    public string? StaleRecoveryReason { get; set; }

    /// <summary>
    /// Tetikleme / yürütme anındaki güvenli yapılandırma özeti (JSON; parola/token yok).
    /// </summary>
    [Column("config_snapshot_json")]
    public string? ConfigSnapshotJson { get; set; }

    public ICollection<BackupArtifact> Artifacts { get; set; } = new List<BackupArtifact>();

    public ICollection<BackupVerification> Verifications { get; set; } = new List<BackupVerification>();
}
