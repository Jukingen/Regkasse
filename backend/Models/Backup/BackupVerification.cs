using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Verification evidence for a backup run. Multiple rows allowed if re-verification is added later.
/// </summary>
[Table("backup_verifications")]
public sealed class BackupVerification
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("backup_run_id")]
    public Guid BackupRunId { get; set; }

    [ForeignKey(nameof(BackupRunId))]
    public BackupRun? BackupRun { get; set; }

    [Required]
    [Column("status")]
    public BackupVerificationStatus Status { get; set; } = BackupVerificationStatus.Pending;

    [Required]
    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Required]
    [MaxLength(80)]
    [Column("verifier_source")]
    public string VerifierSource { get; set; } = string.Empty;

    [Column("completeness_flag")]
    public bool CompletenessFlag { get; set; }

    [MaxLength(4000)]
    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    [Column("details_json")]
    public string? DetailsJson { get; set; }
}
