using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

[Table("finanz_online_outbox_messages")]
public sealed class FinanzOnlineOutboxMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string? TenantId { get; set; }

    [MaxLength(64)]
    public string? BranchId { get; set; }

    [Required]
    [MaxLength(50)]
    public string AggregateType { get; set; } = string.Empty;

    [Required]
    public Guid AggregateId { get; set; }

    [Required]
    [MaxLength(80)]
    public string MessageType { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string BusinessKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "jsonb")]
    public string PayloadJson { get; set; } = "{}";

    [Required]
    [MaxLength(128)]
    public string PayloadHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Mode { get; set; } = "TEST";

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Pending";

    public int AttemptCount { get; set; }

    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

    [MaxLength(80)]
    public string? LastErrorCode { get; set; }

    [MaxLength(500)]
    public string? LastErrorMessage { get; set; }

    [MaxLength(40)]
    public string? FailureCategory { get; set; }

    [MaxLength(120)]
    public string? TransmissionId { get; set; }

    [MaxLength(120)]
    public string? ExternalReferenceId { get; set; }

    [MaxLength(40)]
    public string? ExternalStatus { get; set; }

    [MaxLength(80)]
    public string? ProtocolCode { get; set; }

    [Column(TypeName = "jsonb")]
    public string? LastResponseJson { get; set; }

    [MaxLength(128)]
    public string? ProtocolPayloadHash { get; set; }

    [MaxLength(500)]
    public string? ProtocolSummary { get; set; }

    [MaxLength(64)]
    public string? ProcessingToken { get; set; }

    public DateTime? ProcessingStartedAt { get; set; }

    [MaxLength(120)]
    public string CorrelationId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }
}
