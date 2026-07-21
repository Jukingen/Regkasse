using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>Admin API body for POST /api/admin/payments/{id}/cancel (payment id from route).</summary>
public sealed class CancelPaymentRequest
{
    [Required]
    [MinLength(5)]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;

    [Required]
    public CancellationReasonCode ReasonCode { get; init; }

    public string? ApprovalToken { get; init; }

    [MaxLength(64)]
    public string? IdempotencyKey { get; init; }
}

/// <summary>Admin API body for POST /api/admin/payments/{id}/refund.</summary>
public sealed class RefundPaymentRequest
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; init; }

    [Required]
    [MinLength(5)]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;

    [Required]
    public RefundReasonCode ReasonCode { get; init; }

    public string? ApprovalToken { get; init; }

    [MaxLength(64)]
    public string? IdempotencyKey { get; init; }
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
    public bool RequiresApproval { get; init; }
    public string Operation { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public IReadOnlyList<string> RiskFactors { get; init; } = Array.Empty<string>();
    /// <summary>Primary German reason (first risk factor).</summary>
    public string? Reason { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}

public sealed class PaymentReversalApprovalRequestDto
{
    public Guid ApprovalRequestId { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public bool NotificationSent { get; init; }
}

/// <summary>Admin cancel response for POST /api/admin/payments/{id}/cancel.</summary>
public sealed class CancellationResponse
{
    public bool Success { get; init; }
    public bool RequiresApproval { get; init; }

    /// <summary>Prefer <see cref="PaymentReversalApprovalRequestDto.ApprovalRequestId"/> flows; clients should use <see cref="RequiresApproval"/>.</summary>
    [Obsolete("Unused by FA clients; prefer RequiresApproval + follow-up approval APIs. Planned removal after 2026-12-31.")]
    [JsonPropertyName("approvalId")]
    public Guid? ApprovalId { get; init; }

    public string Message { get; init; } = string.Empty;
    public int? WaitTimeSeconds { get; init; }
    public DateTime? CancelledAt { get; init; }
    public Guid? PaymentId { get; init; }
    public string? DiagnosticCode { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public bool ApprovalNotificationSent { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}

/// <summary>Admin refund response for POST /api/admin/payments/{id}/refund.</summary>
public sealed class RefundResponse
{
    public bool Success { get; init; }
    public bool RequiresApproval { get; init; }

    [Obsolete("Unused by FA clients; prefer RequiresApproval + follow-up approval APIs. Planned removal after 2026-12-31.")]
    [JsonPropertyName("approvalId")]
    public Guid? ApprovalId { get; init; }

    public string Message { get; init; } = string.Empty;
    public int? WaitTimeSeconds { get; init; }
    public DateTime? RefundedAt { get; init; }
    public Guid? PaymentId { get; init; }
    public string? DiagnosticCode { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public bool ApprovalNotificationSent { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}
