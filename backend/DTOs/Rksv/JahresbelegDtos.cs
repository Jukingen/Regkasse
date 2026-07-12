namespace KasseAPI_Final.DTOs.Rksv;

public sealed record JahresbelegResult
{
    public Guid Id { get; init; }

    public Guid CashRegisterId { get; init; }

    public string CashRegisterName { get; init; } = string.Empty;

    public int Year { get; init; }

    public DateTime CreatedAt { get; init; }

    public decimal TotalCash { get; init; }

    public decimal TotalCard { get; init; }

    public decimal TotalVoucher { get; init; }

    public decimal TotalOther { get; init; }

    public decimal TotalGross { get; init; }

    public decimal TotalTax { get; init; }

    public decimal TaxRate20 { get; init; }

    public decimal TaxRate10 { get; init; }

    public decimal TaxRate0 { get; init; }

    public int TransactionCount { get; init; }

    public string? MonthlyReferences { get; init; }

    public string? TseSignature { get; init; }

    public string? TseSignatureTimestamp { get; init; }

    public string? PreviousSignature { get; init; }

    public int SignatureChainLength { get; init; }

    public bool IsSimulated { get; init; }

    public string Environment { get; init; } = string.Empty;

    public bool IsDecemberMonatsbeleg { get; init; }

    public string TseStatusDisplay { get; init; } = string.Empty;
}

public sealed class YearlySummary
{
    public decimal TotalCash { get; set; }

    public decimal TotalCard { get; set; }

    public decimal TotalVoucher { get; set; }

    public decimal TotalOther { get; set; }

    public decimal TotalGross { get; set; }

    public decimal TotalTax { get; set; }

    public decimal TaxRate20 { get; set; }

    public decimal TaxRate10 { get; set; }

    public decimal TaxRate0 { get; set; }

    public int TransactionCount { get; set; }
}

public sealed record JahresbelegSummary
{
    public int Year { get; init; }

    public decimal TotalGross { get; init; }

    public decimal TotalTax { get; init; }

    public int TransactionCount { get; init; }

    public DateTime CreatedAt { get; init; }

    public bool IsSimulated { get; init; }

    public bool HasSignature { get; init; }

    public bool IsDecemberMonatsbeleg { get; init; }
}

/// <summary>Request body for <c>POST /api/rksv/jahresbeleg/create</c> (Phase 3 snapshot).</summary>
public sealed record CreateRksvJahresbelegRequest
{
    public Guid CashRegisterId { get; init; }

    public int Year { get; init; }

    public bool UseDecemberMonatsbeleg { get; init; }
}
