using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Tracks FinanzOnline/BMF submission intent for RKSV Startbeleg and Jahresbeleg special receipts (one row per payment).
/// Does not store credentials or secrets; optional JSON snapshot for non-sensitive protocol excerpts only.
/// </summary>
[Table("rksv_special_receipt_finanz_online_submissions")]
public sealed class RksvSpecialReceiptFinanzOnlineSubmission
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("payment_id")]
    public Guid PaymentId { get; set; }

    [Column("receipt_id")]
    public Guid? ReceiptId { get; set; }

    [Required]
    [Column("cash_register_id")]
    public Guid CashRegisterId { get; set; }

    /// <summary>Startbeleg or Jahresbeleg (matches <see cref="RksvSpecialReceiptKinds"/>).</summary>
    [Required]
    [MaxLength(20)]
    [Column("kind")]
    public string Kind { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    [Column("status")]
    public string Status { get; set; } = RksvSpecialReceiptFinanzOnlineSubmissionStatuses.NotRequired;

    [Column("submitted_at_utc")]
    public DateTime? SubmittedAtUtc { get; set; }

    [Column("verified_at_utc")]
    public DateTime? VerifiedAtUtc { get; set; }

    [Column("last_attempt_at_utc")]
    public DateTime? LastAttemptAtUtc { get; set; }

    [Column("attempt_count")]
    public int AttemptCount { get; set; }

    [MaxLength(80)]
    [Column("last_error_code")]
    public string? LastErrorCode { get; set; }

    [MaxLength(500)]
    [Column("last_error_message")]
    public string? LastErrorMessage { get; set; }

    [MaxLength(120)]
    [Column("external_reference")]
    public string? ExternalReference { get; set; }

    [Column("raw_response_snapshot", TypeName = "jsonb")]
    public string? RawResponseSnapshot { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime? UpdatedAtUtc { get; set; }
}
