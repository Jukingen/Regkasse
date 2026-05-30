using KasseAPI_Final.Models;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>Admin payment list row.</summary>
public class PaymentListItemDto
{
    public Guid Id { get; set; }
    public string? TransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Method { get; set; } = "Unknown";
    public string Status { get; set; } = "Success";
    public string? CustomerName { get; set; }
    public Guid CashRegisterId { get; set; }
    public string? ReceiptNumber { get; set; }
    public Guid? ReceiptId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public bool IsOfflineOrigin { get; set; }
    public Guid? OfflineTransactionId { get; set; }
    public Guid? OfflineReplayBatchCorrelationId { get; set; }
    public string? FinanzOnlineStatus { get; set; }
    public string? FinanzOnlineError { get; set; }
    public string? FinanzOnlineReferenceId { get; set; }
    public DateTime? FinanzOnlineLastAttemptAtUtc { get; set; }
    public int FinanzOnlineRetryCount { get; set; }
    public bool InvoicePersisted { get; set; }
    public decimal VoucherRedeemedAmount { get; set; }
    public bool HasVoucherRedemption { get; set; }
    public bool IsStorno { get; set; }
    public bool IsRefund { get; set; }
    public StornoReason? StornoReason { get; set; }
    public Guid? OriginalPaymentId { get; set; }
    public string? OriginalReceiptNumber { get; set; }
    public string? CashierDisplayName { get; set; }
    public string ReversalCompletionStatus { get; set; } = "Completed";
}

/// <summary>OpenAPI-stable alias for <see cref="PaymentListItemDto"/>.</summary>
public class AdminPaymentListItemDto : PaymentListItemDto;
