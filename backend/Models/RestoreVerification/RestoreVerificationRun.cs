using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KasseAPI_Final.Models.Backup;
namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>
/// Restore drill metadata: distinct from backup artifact verification (checksum / staging). TSE vendor restore deferred.
/// </summary>
[Table("restore_verification_runs")]
public sealed class RestoreVerificationRun : KasseAPI_Final.Models.IRunLeaseColumns
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("status")]
    public RestoreVerificationStatus Status { get; set; } = RestoreVerificationStatus.Queued;

    [Required]
    [Column("trigger_source")]
    public RestoreVerificationTriggerSource TriggerSource { get; set; }

    [Column("source_backup_run_id")]
    public Guid? SourceBackupRunId { get; set; }

    [ForeignKey(nameof(SourceBackupRunId))]
    public BackupRun? SourceBackupRun { get; set; }

    /// <summary>Internal relative file name or locator for ops logs only; not exposed via redacted API.</summary>
    [MaxLength(512)]
    [Column("dump_relative_descriptor")]
    public string? DumpRelativeDescriptor { get; set; }

    /// <summary><c>pg_restore --list</c> / TOC incelemesi (artifact checksum değil).</summary>
    [Column("pg_restore_list_passed")]
    public bool? PgRestoreListPassed { get; set; }

    [Column("pg_restore_list_exit_code")]
    public int? PgRestoreListExitCode { get; set; }

    [Column("pg_restore_list_line_count")]
    public int? PgRestoreListLineCount { get; set; }

    /// <summary>İzole geçici veritabanına gerçek <c>pg_restore</c> denemesi yapıldı mı.</summary>
    [Column("restore_attempt_executed")]
    public bool RestoreAttemptExecuted { get; set; }

    /// <summary>Restore denemesi sonucu; null = çalıştırılmadı veya henüz bilinmiyor.</summary>
    [Column("restore_attempt_passed")]
    public bool? RestoreAttemptPassed { get; set; }

    [Column("restore_attempt_exit_code")]
    public int? RestoreAttemptExitCode { get; set; }

    [MaxLength(150)]
    [Column("restore_attempt_skip_reason")]
    public string? RestoreAttemptSkipReason { get; set; }

    /// <summary>Örn. <c>rv_v_&lt;runFragment&gt;</c>; sunucu host/parola içermez.</summary>
    [MaxLength(80)]
    [Column("restore_target_db_redacted")]
    public string? RestoreTargetDbRedacted { get; set; }

    /// <summary>True when fiscal script was not run (missing config, production safety, or missing file).</summary>
    [Column("fiscal_sql_skipped")]
    public bool FiscalSqlSkipped { get; set; }

    [MaxLength(100)]
    [Column("fiscal_sql_skip_reason")]
    public string? FiscalSqlSkipReason { get; set; }

    [Column("fiscal_sql_passed")]
    public bool? FiscalSqlPassed { get; set; }

    [Column("fiscal_sql_fail_count")]
    public int? FiscalSqlFailCount { get; set; }

    [Column("fiscal_sql_warn_count")]
    public int? FiscalSqlWarnCount { get; set; }

    /// <summary>Live operational DB read-only integrity (not post-restore unless target is restored copy).</summary>
    [MaxLength(64)]
    [Column("integrity_scope")]
    public string? IntegrityScope { get; set; }

    [Column("integrity_checks_passed")]
    public bool? IntegrityChecksPassed { get; set; }

    [Required]
    [Column("requested_at")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

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

    [Column("details_json")]
    public string? DetailsJson { get; set; }

    [MaxLength(450)]
    [Column("requested_by_user_id")]
    public string? RequestedByUserId { get; set; }

    [MaxLength(100)]
    [Column("correlation_id")]
    public string? CorrelationId { get; set; }

    /// <summary>İsteğe bağlı manuel tetik idempotency anahtarı; zamanlanmış satırlarda null.</summary>
    [MaxLength(200)]
    [Column("idempotency_key")]
    public string? IdempotencyKey { get; set; }

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
    /// Kuyruk / yürütme anındaki güvenli restore drill yapılandırması (JSON; parola/token yok).
    /// </summary>
    [Column("config_snapshot_json")]
    public string? ConfigSnapshotJson { get; set; }
}
