using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Peppol send outbox. No UBL payload. The document table is not in this schema.
/// </summary>
[Table("einvoice_submissions")]
public class EinvoiceSubmission : ITenantEntity
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("invoice_id")]
    public Guid InvoiceId { get; set; }

    /// <summary><c>Queued</c>, <c>Sent</c>, <c>Ack</c>, or <c>Failed</c>.</summary>
    [Column("status")]
    [MaxLength(16)]
    public string Status { get; set; } = EinvoiceSubmissionStatuses.Queued;

    [Column("correlation_id")]
    public Guid CorrelationId { get; set; }

    [Column("attempted_at_utc")]
    public DateTime? AttemptedAtUtc { get; set; }

    [Column("acked_at_utc")]
    public DateTime? AckedAtUtc { get; set; }

    [Column("failure_reason")]
    [MaxLength(512)]
    public string? FailureReason { get; set; }

    /// <summary>Storecove <c>guid</c>. Not an ACK.</summary>
    [Column("provider_message_id")]
    [MaxLength(255)]
    public string? ProviderMessageId { get; set; }

    /// <summary>Storecove <c>status</c> text, or the HTTP status when the body has none.</summary>
    [Column("provider_status")]
    [MaxLength(64)]
    public string? ProviderStatus { get; set; }

    /// <summary>How many transient ACK status reads have been scheduled. Not an ACK.</summary>
    [Column("provider_attempt_count")]
    public int ProviderAttemptCount { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }
}

public static class EinvoiceSubmissionStatuses
{
    public const string Queued = "Queued";
    public const string Sent = "Sent";
    public const string Ack = "Ack";
    public const string Failed = "Failed";
}
