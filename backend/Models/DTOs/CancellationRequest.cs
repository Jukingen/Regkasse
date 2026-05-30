using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Models.DTOs;

public class CancellationRequest
{
    [Required]
    public string PaymentId { get; set; } = string.Empty;

    [Required]
    [MinLength(5)]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public CancellationReasonCode ReasonCode { get; set; }

    /// <summary>6-digit manager approval token when the operation is high-risk.</summary>
    public string? ApprovalToken { get; set; }

    public bool RequiresApproval { get; set; }
}

public class RefundRequest
{
    [Required]
    public string PaymentId { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    [MinLength(5)]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public RefundReasonCode ReasonCode { get; set; }

    public string? ApprovalToken { get; set; }

    public bool RequiresApproval { get; set; }
}

/// <summary>Admin API body for POST /api/admin/payments/{id}/cancel (payment id from route).</summary>
public class CancelPaymentRequest
{
    [Required]
    [MinLength(5)]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public CancellationReasonCode ReasonCode { get; set; }

    public string? ApprovalToken { get; set; }

    [MaxLength(64)]
    public string? IdempotencyKey { get; set; }
}

/// <summary>Admin API body for POST /api/admin/payments/{id}/refund.</summary>
public class RefundPaymentRequest
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    [MinLength(5)]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public RefundReasonCode ReasonCode { get; set; }

    public string? ApprovalToken { get; set; }

    [MaxLength(64)]
    public string? IdempotencyKey { get; set; }
}

public enum CancellationReasonCode
{
    CustomerRequest = 1,
    WrongItem = 2,
    PriceMismatch = 3,
    Duplicate = 4,
    TechnicalError = 5,
    Other = 99
}

public enum RefundReasonCode
{
    CustomerComplaint = 1,
    WrongProduct = 2,
    QualityIssue = 3,
    Overcharged = 4,
    Other = 99
}

public sealed class PaymentReversalPolicyDto
{
    public bool RequiresApproval { get; set; }
    public string Operation { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public IReadOnlyList<string> RiskFactors { get; set; } = Array.Empty<string>();
    /// <summary>Primary German reason (first risk factor).</summary>
    public string? Reason { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
}

public sealed class PaymentReversalApprovalRequestDto
{
    public Guid ApprovalRequestId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool NotificationSent { get; set; }
}

/// <summary>Admin cancel response for POST /api/admin/payments/{id}/cancel.</summary>
public sealed class CancellationResponse
{
    public bool Success { get; set; }
    public bool RequiresApproval { get; set; }
    public Guid? ApprovalId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? WaitTimeSeconds { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? PaymentId { get; set; }
    public string? DiagnosticCode { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    public bool ApprovalNotificationSent { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
}

/// <summary>Admin refund response for POST /api/admin/payments/{id}/refund.</summary>
public sealed class RefundResponse
{
    public bool Success { get; set; }
    public bool RequiresApproval { get; set; }
    public Guid? ApprovalId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? WaitTimeSeconds { get; set; }
    public DateTime? RefundedAt { get; set; }
    public Guid? PaymentId { get; set; }
    public string? DiagnosticCode { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    public bool ApprovalNotificationSent { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
}
