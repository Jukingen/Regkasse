namespace KasseAPI_Final.Services.Reports;

/// <summary>
/// Display-only sales rollup for report PDFs. It is not part of the RKSV signature payload.
/// </summary>
public sealed class PeriodSalesSummary
{
    public const string ZeroSalesNote = "In diesem Zeitraum wurden keine Umsätze erfasst.";

    public string PeriodLabel { get; init; } = string.Empty;

    public int ReceiptCount { get; init; }

    public decimal TotalRevenue { get; init; }

    public decimal NetAmount { get; init; }

    public decimal Vat10 { get; init; }

    public decimal Vat20 { get; init; }

    public decimal Vat13 { get; init; }

    public decimal Vat49 { get; init; }

    public decimal GrossAmount { get; init; }

    public IReadOnlyList<PeriodSalesPaymentTotal> PaymentMethods { get; init; } =
        Array.Empty<PeriodSalesPaymentTotal>();

    public bool ZeroSales => ReceiptCount == 0 || TotalRevenue == 0m;
}

public sealed class PeriodSalesPaymentTotal
{
    public string Label { get; init; } = string.Empty;

    public decimal Amount { get; init; }
}
