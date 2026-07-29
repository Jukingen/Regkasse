namespace KasseAPI_Final.Models;

/// <summary>
/// RKSV historical period report built from receipt tax lines captured at sale time
/// (never from current product catalog prices/rates).
/// </summary>
public sealed class RksvReport
{
    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public List<RksvTransaction> Transactions { get; set; } = [];

    /// <summary>Tax rate (%) → sum of VAT amounts for that historical rate.</summary>
    public Dictionary<decimal, decimal> TaxBreakdown { get; set; } = new();

    public decimal TotalNet { get; set; }

    public decimal TotalTax { get; set; }

    public decimal TotalGross { get; set; }

    public bool IsCompliant { get; set; }

    public List<ComplianceWarning> Warnings { get; set; } = [];
}

/// <summary>
/// One historical VAT bucket from a receipt (rate frozen at issuance).
/// Multi-rate receipts produce multiple rows.
/// </summary>
public sealed class RksvTransaction
{
    public Guid ReceiptId { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; }

    /// <summary>Net amount for this tax rate line (transaction-time).</summary>
    public decimal Amount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal GrossAmount { get; set; }

    /// <summary>VAT rate (%) stored on the receipt tax line at sale time.</summary>
    public decimal TaxRate { get; set; }

    public string TaxGroupName { get; set; } = string.Empty;

    public string? TseSignature { get; set; }
}

/// <summary>Day/period tax breakdown using historical receipt rates only.</summary>
public sealed class TaxBreakdown
{
    public DateTime Date { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    /// <summary>Tax rate (%) → VAT amount.</summary>
    public Dictionary<decimal, decimal> ByRate { get; set; } = new();

    public decimal TotalNet { get; set; }

    public decimal TotalTax { get; set; }

    public decimal TotalGross { get; set; }

    public int ReceiptCount { get; set; }
}

public sealed class PriceHistoryReport
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int CatalogVersion { get; set; }

    public Guid? OriginalProductId { get; set; }

    public bool IsArchived { get; set; }

    public List<PriceHistoryReportEntry> History { get; set; } = [];

    public List<PriceVersionReportEntry> Versions { get; set; } = [];
}

public sealed class PriceHistoryReportEntry
{
    public Guid Id { get; set; }

    public decimal OldPrice { get; set; }

    public decimal NewPrice { get; set; }

    public Guid OldTaxGroupId { get; set; }

    public Guid NewTaxGroupId { get; set; }

    public decimal OldTaxRate { get; set; }

    public decimal NewTaxRate { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; }

    public string Reason { get; set; } = string.Empty;

    public bool IsRksvCompliant { get; set; }
}

public sealed class PriceVersionReportEntry
{
    public Guid Id { get; set; }

    public decimal Price { get; set; }

    public Guid TaxGroupId { get; set; }

    public string? TaxGroupName { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public bool IsCurrent { get; set; }

    public string Version { get; set; } = string.Empty;
}

public sealed class ComplianceWarning
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid? ReceiptId { get; set; }

    public string? ReceiptNumber { get; set; }
}
