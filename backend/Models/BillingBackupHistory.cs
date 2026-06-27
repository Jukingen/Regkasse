using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Platform-scoped billing invoice archive run history (Super Admin billing).</summary>
[Table("billing_backup_history")]
public class BillingBackupHistory
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("backup_run_id")]
    [MaxLength(50)]
    public string BackupRunId { get; set; } = string.Empty;

    [Column("sale_id")]
    public Guid? SaleId { get; set; }

    [ForeignKey(nameof(SaleId))]
    public virtual LicenseSale? Sale { get; set; }

    /// <summary>sale, daily, weekly, full — see <see cref="BillingBackupTypes"/>.</summary>
    [Column("backup_type")]
    [MaxLength(20)]
    public string BackupType { get; set; } = string.Empty;

    [Column("backup_path")]
    [MaxLength(500)]
    public string BackupPath { get; set; } = string.Empty;

    [Column("file_size_bytes")]
    public long FileSizeBytes { get; set; }

    [Column("file_hash")]
    [MaxLength(64)]
    public string FileHash { get; set; } = string.Empty;

    [Column("record_count")]
    public int RecordCount { get; set; }

    /// <summary>success, failed, partial — see <see cref="BillingBackupStatuses"/>.</summary>
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = BillingBackupStatuses.Success;

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("triggered_by_user_id")]
    public Guid? TriggeredByUserId { get; set; }

    [Column("started_at_utc")]
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("completed_at_utc")]
    public DateTime? CompletedAtUtc { get; set; }

    [Column("retention_until_utc")]
    public DateTime? RetentionUntilUtc { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
