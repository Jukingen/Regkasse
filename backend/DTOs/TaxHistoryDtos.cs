namespace KasseAPI_Final.DTOs;

public sealed class TaxHistoryItemDto
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public Guid TaxGroupId { get; set; }

    public string? TaxGroupName { get; set; }

    public decimal OldRate { get; set; }

    public decimal NewRate { get; set; }

    public DateTime ChangedAt { get; set; }

    public Guid ChangedBy { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? InvoiceNumber { get; set; }
}

public sealed class RecordTaxHistoryRequest
{
    public Guid ProductId { get; set; }

    public Guid TaxGroupId { get; set; }

    public decimal OldRate { get; set; }

    public decimal NewRate { get; set; }

    public Guid ChangedBy { get; set; }

    public string? Reason { get; set; }

    public string? InvoiceNumber { get; set; }
}
