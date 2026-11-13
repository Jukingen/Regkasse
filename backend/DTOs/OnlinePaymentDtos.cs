using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class AdminOnlinePaymentDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public string TenantSlug { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "EUR";
    public string Status { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string? PaymentIntentId { get; init; }
    public Guid? OnlineOrderId { get; init; }
    public string? OrderNumber { get; init; }
    public bool IsSynthetic { get; init; }
    public string? ErrorMessage { get; init; }
    public string? LastWebhookEvent { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
}

public sealed class AdminOnlinePaymentListResponse
{
    public IReadOnlyList<AdminOnlinePaymentDto> Items { get; init; } = Array.Empty<AdminOnlinePaymentDto>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
}

public sealed class AdminOnlinePaymentTestRequest
{
    /// <summary>
    /// <c>create</c>, <c>webhookSucceeded</c>/<c>success</c>, <c>webhookFailed</c>/<c>failed</c>, or <c>expire</c>.
    /// </summary>
    public string Action { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public string? PaymentMethod { get; init; }
    public Guid? TransactionId { get; init; }

    /// <summary>POS <c>gateway_payment_intents</c> row to simulate (alias of <see cref="TransactionId"/>).</summary>
    public Guid? OnlinePaymentId { get; init; }
}

public sealed class AdminOnlinePaymentTestResponse
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public AdminOnlinePaymentDto? Transaction { get; init; }
    public OnlinePaymentDto? PosPayment { get; init; }
}

/// <summary>POS online payment initiation (card / PayPal hosted or intent flow).</summary>
public sealed class InitiateOnlinePaymentRequest
{
    [Required]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "EUR";

    [Required]
    public Guid CashRegisterId { get; set; }

    /// <summary>Online method: <c>card</c> or <c>paypal</c>.</summary>
    [Required]
    [MaxLength(32)]
    public string Method { get; set; } = "card";

    [MaxLength(64)]
    public string? IdempotencyKey { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Optional return URL after hosted checkout (PayPal / redirect providers).</summary>
    [MaxLength(500)]
    public string? ReturnUrl { get; set; }

    /// <summary>Optional cancel URL for hosted checkout. Falls back to <see cref="ReturnUrl"/>.</summary>
    [MaxLength(500)]
    public string? CancelUrl { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class OnlinePaymentDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public Guid CashRegisterId { get; set; }
    public string? PaymentIntentId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RedirectUrl { get; set; }
    public string? TransactionId { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? PaymentDetailsId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

/// <summary>Admin test-console simulation of a POS gateway success or failure.</summary>
public sealed class SimulateOnlinePaymentRequest
{
    [Required]
    public Guid OnlinePaymentId { get; set; }

    /// <summary><c>success</c> or <c>failed</c>.</summary>
    [Required]
    [MaxLength(16)]
    public string Outcome { get; set; } = "success";
}

public sealed class PaymentWebhookReceivedResponse
{
    public bool Received { get; set; } = true;
    public string? Status { get; set; }
    public Guid? OnlinePaymentId { get; set; }
}
