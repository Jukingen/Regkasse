using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>
/// Super-admin manual restore with second-admin approval. Never targets production <c>DefaultConnection</c> database.
/// </summary>
[Table("manual_restore_requests")]
public sealed class ManualRestoreRequest
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("status")]
    public ManualRestoreRequestStatus Status { get; set; } = ManualRestoreRequestStatus.PendingApproval;

    [Required]
    [Column("backup_run_id")]
    public Guid BackupRunId { get; set; }

    [ForeignKey(nameof(BackupRunId))]
    public BackupRun? BackupRun { get; set; }

    [Required]
    [MaxLength(63)]
    [Column("target_database_name")]
    public string TargetDatabaseName { get; set; } = string.Empty;

    [Column("validation_only")]
    public bool ValidationOnly { get; set; } = true;

    [MaxLength(2000)]
    [Column("reason")]
    public string? Reason { get; set; }

    /// <summary>BCrypt hash of the 6-digit approval token.</summary>
    [MaxLength(100)]
    [Column("approval_token_hash")]
    public string? ApprovalTokenHash { get; set; }

    [Column("approval_token_expires_at_utc")]
    public DateTime? ApprovalTokenExpiresAtUtc { get; set; }

    [Required]
    [Column("requested_at")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("requested_by_user_id")]
    public string? RequestedByUserId { get; set; }

    [MaxLength(256)]
    [Column("requested_by_email")]
    public string? RequestedByEmail { get; set; }

    [MaxLength(450)]
    [Column("approved_by_user_id")]
    public string? ApprovedByUserId { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [MaxLength(2000)]
    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [MaxLength(4000)]
    [Column("result")]
    public string? Result { get; set; }

    [Column("restore_verification_run_id")]
    public Guid? RestoreVerificationRunId { get; set; }

    [ForeignKey(nameof(RestoreVerificationRunId))]
    public RestoreVerificationRun? RestoreVerificationRun { get; set; }

    [MaxLength(100)]
    [Column("correlation_id")]
    public string? CorrelationId { get; set; }
}
