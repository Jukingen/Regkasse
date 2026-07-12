namespace KasseAPI_Final.Models.Reports;

/// <summary>Read-only yearly aggregation preview from Monatsbeleg rows.</summary>
public sealed class JahresbelegSummaryDto
{
    public int Year { get; init; }

    public Guid CashRegisterId { get; init; }

    public int MonatsbelegCount { get; init; }

    public IReadOnlyList<JahresbelegMonthlyReferenceDto> MonthlyReferences { get; init; } =
        Array.Empty<JahresbelegMonthlyReferenceDto>();

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

    public PaymentBreakdown PaymentBreakdown { get; init; } = new();

    public DailyClosingTaxBreakdownDto TaxBreakdown { get; init; } = new();

    public TransactionBreakdown TransactionBreakdown { get; init; } = new();

    public bool IsDecemberMonatsbeleg { get; init; }

    public IReadOnlyList<int> MissingMonths { get; init; } = Array.Empty<int>();
}

public sealed class JahresbelegMonthlyReferenceDto
{
    public int Year { get; init; }

    public int Month { get; init; }

    public Guid Id { get; init; }
}
