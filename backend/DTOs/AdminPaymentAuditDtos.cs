namespace KasseAPI_Final.DTOs;

/// <summary>Line-level fiscal comparison for storno/refund audit (parsed from payment_details.payment_items JSON).</summary>
public sealed class AdminPaymentAuditLineItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal TaxAmount { get; set; }
}

public sealed class AdminPaymentAuditEventDto
{
    public DateTime TimestampUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? HttpStatusCode { get; set; }
}

public sealed class AdminPaymentStornoRefundAuditDto
{
    public Guid? OriginalPaymentId { get; set; }
    public string? OriginalReceiptNumber { get; set; }
    public DateTime? OriginalCreatedAtUtc { get; set; }
    public decimal? OriginalTotalAmount { get; set; }
    public IReadOnlyList<AdminPaymentAuditLineItemDto> OriginalLineItems { get; set; } = Array.Empty<AdminPaymentAuditLineItemDto>();
    public IReadOnlyList<AdminPaymentAuditLineItemDto> ReversalLineItems { get; set; } = Array.Empty<AdminPaymentAuditLineItemDto>();
    public double? SecondsBetweenOriginalAndReversal { get; set; }
    public IReadOnlyList<AdminPaymentAuditEventDto> RelatedAuditEvents { get; set; } = Array.Empty<AdminPaymentAuditEventDto>();
}
