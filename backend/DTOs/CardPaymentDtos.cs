namespace KasseAPI_Final.DTOs;

/// <summary>POS card payment intent creation (spec-aligned).</summary>
public sealed class CardPaymentRequest
{
    public decimal Amount { get; set; }
    public Guid CashRegisterId { get; set; }
    public string? ReceiptNumber { get; set; }
}

public sealed class CreateCardPaymentIntentRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public Guid CashRegisterId { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>POS card payment confirm (spec-aligned).</summary>
public sealed class ConfirmCardPaymentRequest
{
    /// <summary>Internal intent id (Guid) or gateway payment intent id (e.g. pi_xxx).</summary>
    public string PaymentIntentId { get; set; } = string.Empty;

    /// <summary>Simulated card number or Stripe payment method id (pm_xxx).</summary>
    public string? PaymentMethodId { get; set; }
}

public sealed class ConfirmCardPaymentIntentRequest
{
    /// <summary>Simulated card number or Stripe payment method id (pm_xxx).</summary>
    public string PaymentMethodId { get; set; } = string.Empty;
}

public sealed class CardPaymentIntentResponse
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Status { get; set; } = string.Empty;
    public string GatewayProvider { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    /// <summary>Gateway charge id when confirmed; on create equals <see cref="Id"/> for spec clients.</summary>
    public string? TransactionId { get; set; }
    public string? CardBrand { get; set; }
    public string? LastFourDigits { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid CashRegisterId { get; set; }
    public Guid? PaymentDetailsId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
}

public sealed class CardPaymentConfirmResponse
{
    public bool Success { get; set; }
    public Guid? TransactionId { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class AdminCardTransactionListItemDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Status { get; set; } = string.Empty;
    public string GatewayProvider { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? CardBrand { get; set; }
    public string? LastFourDigits { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid CashRegisterId { get; set; }
    public string? CashRegisterLabel { get; set; }
    public Guid? PaymentDetailsId { get; set; }
    public string? ReceiptNumber { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public decimal? RefundedAmount { get; set; }
}

public sealed class AdminCardTransactionListResponse
{
    public IReadOnlyList<AdminCardTransactionListItemDto> Items { get; set; } = Array.Empty<AdminCardTransactionListItemDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public sealed class AdminCardTransactionFilterDto
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Status { get; set; }
    public Guid? CashRegisterId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
