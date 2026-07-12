namespace KasseAPI_Final.DTOs.Rksv;

public sealed record MonatsbelegResult
{
    public Guid Id { get; init; }

    public Guid CashRegisterId { get; init; }

    public string CashRegisterName { get; init; } = string.Empty;

    public int Year { get; init; }

    public int Month { get; init; }

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

    public int DailyClosingCount { get; init; }

    public string? TseSignature { get; init; }

    public string? TseSignatureTimestamp { get; init; }

    public string? TseCertificateThumbprint { get; init; }

    public string? PreviousSignature { get; init; }

    public int SignatureChainLength { get; init; }

    public bool IsSimulated { get; init; }

    public string Environment { get; init; } = string.Empty;

    public string TseStatusDisplay { get; init; } = string.Empty;
}

public sealed class MonthlySummary
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

    public int DailyClosingCount { get; set; }
}

public sealed record MonatsbelegSummary
{
    public int Year { get; init; }

    public int Month { get; init; }

    public decimal TotalGross { get; init; }

    public decimal TotalTax { get; init; }

    public int TransactionCount { get; init; }

    public DateTime CreatedAt { get; init; }

    public bool IsSimulated { get; init; }

    public bool HasSignature { get; init; }
}

/// <summary>Request body for <c>POST /api/rksv/monatsbeleg/create</c> (Phase 2 snapshot).</summary>
public sealed record CreateRksvMonatsbelegRequest
{
    public Guid CashRegisterId { get; init; }

    public int Year { get; init; }

    public int Month { get; init; }
}
