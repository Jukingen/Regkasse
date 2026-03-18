namespace KasseAPI_Final.Models.Export;

/// <summary>Root payload for fiscal / RKSV export (JSON; CSV fragments optional).</summary>
public sealed class FiscalExportPackageDto
{
    public string SchemaVersion { get; set; } = "1.0";
    public DateTime GeneratedAtUtc { get; set; }
    public Guid CashRegisterId { get; set; }
    public string RegisterNumber { get; set; } = string.Empty;
    public string RegisterLocation { get; set; } = string.Empty;
    public FiscalExportPeriodDto Period { get; set; } = new();
    /// <summary>Current TSE chain head for this register (one row per register).</summary>
    public FiscalSignatureChainStateDto? SignatureChainState { get; set; }
    /// <summary>Ordered receipt chain fields (prev → current) for the period.</summary>
    public IReadOnlyList<FiscalReceiptChainLinkDto> ReceiptChain { get; set; } = Array.Empty<FiscalReceiptChainLinkDto>();
    public IReadOnlyList<FiscalReceiptExportDto> Receipts { get; set; } = Array.Empty<FiscalReceiptExportDto>();
    public IReadOnlyList<FiscalClosingExportDto> Closings { get; set; } = Array.Empty<FiscalClosingExportDto>();
    public string? ReceiptsCsv { get; set; }
    public string? ClosingsCsv { get; set; }
    public int ReceiptCount { get; set; }
    public int ClosingCount { get; set; }
}

public sealed class FiscalExportPeriodDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
}

public sealed class FiscalSignatureChainStateDto
{
    public Guid CashRegisterId { get; set; }
    public string? LastSignature { get; set; }
    public int LastCounter { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class FiscalReceiptChainLinkDto
{
    public string ReceiptNumber { get; set; } = string.Empty;
    public Guid ReceiptId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public string? SignatureValue { get; set; }
    public string? PrevSignatureValue { get; set; }
}

public sealed class FiscalReceiptExportDto
{
    public Guid ReceiptId { get; set; }
    public Guid PaymentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public string? CashierId { get; set; }
    public Guid CashRegisterId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public string? QrCodePayload { get; set; }
    public string? SignatureValue { get; set; }
    public string? PrevSignatureValue { get; set; }
    public string? SignatureFormat { get; set; }
    public string? JwsHeader { get; set; }
    public string? JwsPayload { get; set; }
    public string? JwsSignature { get; set; }
    public string? Provider { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public IReadOnlyList<FiscalReceiptItemExportDto> Items { get; set; } = Array.Empty<FiscalReceiptItemExportDto>();
    public IReadOnlyList<FiscalReceiptTaxLineExportDto> TaxLines { get; set; } = Array.Empty<FiscalReceiptTaxLineExportDto>();
}

public sealed class FiscalReceiptItemExportDto
{
    public Guid ItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal LineNet { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TaxRate { get; set; }
    public Guid? ParentItemId { get; set; }
    public string? CategoryName { get; set; }
}

public sealed class FiscalReceiptTaxLineExportDto
{
    public Guid LineId { get; set; }
    public int TaxType { get; set; }
    public decimal TaxRate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
}

public sealed class FiscalClosingExportDto
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime ClosingDateUtc { get; set; }
    public string ClosingType { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public int TransactionCount { get; set; }
    public string TseSignature { get; set; } = string.Empty;
    public string? SignatureFormat { get; set; }
    public string? JwsHeader { get; set; }
    public string? JwsPayload { get; set; }
    public string? JwsSignature { get; set; }
    public string? Provider { get; set; }
    public string? CorrelationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FinanzOnlineStatus { get; set; }
    public string? FinanzOnlineReferenceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
