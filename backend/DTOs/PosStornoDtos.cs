using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class StornoRequest
{
    [Required]
    public Guid PaymentId { get; set; }

    [Required]
    [MinLength(5)]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Stable code from payment history (e.g. CUSTOMER_REQUEST) or <see cref="Models.DTOs.CancellationReasonCode"/> name.</summary>
    [Required]
    public string ReasonCode { get; set; } = string.Empty;

    public string? ApprovalToken { get; set; }

    [MaxLength(64)]
    public string? IdempotencyKey { get; set; }
}

public sealed class StornoResponse
{
    public bool Success { get; set; }
    public string? ErrorKey { get; set; }
    public string? MessageKey { get; set; }
    public Guid? StornoPaymentId { get; set; }
    public bool RequiresApproval { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public DateTime? ApprovalTokenExpiresAtUtc { get; set; }
    public string? DiagnosticCode { get; set; }
}
