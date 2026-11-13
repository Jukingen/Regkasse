namespace KasseAPI_Final.DTOs;

public sealed class PeriodSalesSummaryDto
{
    public string PeriodLabel { get; init; } = string.Empty;

    public int ReceiptCount { get; init; }

    public decimal TotalRevenue { get; init; }

    public decimal NetAmount { get; init; }

    public decimal Vat10 { get; init; }

    public decimal Vat20 { get; init; }

    public decimal GrossAmount { get; init; }

    public bool ZeroSales { get; init; }

    /// <summary>Signed Monatsbeleg or Jahresbeleg payment for this period, when one exists.</summary>
    public Guid? PaymentId { get; init; }
}
