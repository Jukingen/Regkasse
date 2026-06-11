namespace KasseAPI_Final.Models.DTOs;

/// <summary>POS last-24h payment history row with backend-controlled reversal actions.</summary>
public sealed class PaymentHistoryItemDto
{
    public Guid Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int? TableNumber { get; set; }
    public bool IsStorno { get; set; }
    public bool IsRefund { get; set; }

    /// <summary>Backend-decided actions (storno / refund / view_only) with i18n label keys.</summary>
    public List<AvailableAction> AvailableActions { get; set; } = new();
}

public sealed class AvailableAction
{
    /// <summary>Machine action id: <c>storno</c>, <c>refund</c>, or <c>view_only</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Frontend i18n key (e.g. paymentHistory.actions.storno).</summary>
    public string LabelKey { get; set; } = string.Empty;

    public bool RequiresReason { get; set; }
    public bool RequiresManagerApproval { get; set; }
    public string? ReasonLabelKey { get; set; }
    public List<ReasonOption> ReasonOptions { get; set; } = new();
}

public sealed class ReasonOption
{
    public string Code { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
}

public sealed class PaymentHistoryResponse
{
    public List<PaymentHistoryItemDto> Payments { get; set; } = new();
    public int TotalCount { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
    public bool HasMore { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public Guid CashRegisterId { get; set; }
    public string Language { get; set; } = "de";
}
