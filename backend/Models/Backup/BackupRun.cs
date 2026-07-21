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

    /// <summary>
    /// Owning tenant when the run is tenant-scoped (manual/import). Null for deployment-wide scheduled or all-tenant runs.
    /// Not <see cref="ITenantEntity"/> — explicit access filters only (nullable deployment rows).
    /// </summary>
    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Product strategy (TenantAdmin vs SuperAdmin). Independent of dump physics:
    /// both may still be instance-wide <c>pg_dump</c>; see <see cref="BackupStrategyKind"/>.
    /// Defaults to <see cref="BackupStrategyKind.Tenant"/> — System (Identity / all-tenants) must be set explicitly.
    /// </summary>
    [Required]
    [Column("strategy")]
    public BackupStrategyKind Strategy { get; set; } = BackupStrategyKind.Tenant;

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

    /// <summary>Otomatik yeniden kuyruğa alma tur sayısı (ilk deneme 0; her başarılı requeue sonrası artar).</summary>
    [Column("automatic_retry_count")]
    public int AutomaticRetryCount { get; set; }

    /// <summary>Worker bu zaman UTC’de veya sonrasında <see cref="BackupRunStatus.Queued"/> yapabilir; null ise plan yok.</summary>
    [Column("next_retry_at_utc")]
    public DateTime? NextRetryAtUtc { get; set; }

    /// <summary>Son terminal başarısızlık kodu (Succeeded sonrası temizlenir); gözlemlenebilirlik.</summary>
    [MaxLength(100)]
    [Column("last_recorded_terminal_failure_code")]
    public string? LastRecordedTerminalFailureCode { get; set; }

    /// <summary>
    /// Bekleyen otomatik requeue için sınıflandırılmış neden (İngilizce sabit; BackupFailureRetryClassifier).
    /// </summary>
    [MaxLength(80)]
    [Column("automatic_retry_pending_classified_reason")]
    public string? AutomaticRetryPendingClassifiedReason { get; set; }

    /// <summary><see cref="NextRetryAtUtc"/> son ayarlandığında UTC (operatör gözlemi).</summary>
    [Column("automatic_retry_last_scheduled_at_utc")]
    public DateTime? AutomaticRetryLastScheduledAtUtc { get; set; }

    public ICollection<BackupArtifact> Artifacts { get; set; } = new List<BackupArtifact>();

    public ICollection<BackupVerification> Verifications { get; set; } = new List<BackupVerification>();
}
