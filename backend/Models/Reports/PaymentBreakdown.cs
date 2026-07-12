namespace KasseAPI_Final.Models.Reports;

/// <summary>Payment-method amounts for RKSV daily closing reports.</summary>
public sealed class PaymentBreakdown
{
    public decimal Cash { get; set; }

    public decimal Card { get; set; }

    public decimal Voucher { get; set; }

    public decimal Other { get; set; }

    public decimal Total { get; set; }

    public static PaymentBreakdown FromAmounts(decimal cash, decimal card, decimal voucher, decimal other) =>
        new()
        {
            Cash = cash,
            Card = card,
            Voucher = voucher,
            Other = other,
            Total = cash + card + voucher + other,
        };

    /// <summary>Sample breakdown for demo/dev previews when no live payment rows exist.</summary>
    public static PaymentBreakdown CreateDemo() =>
        new()
        {
            Cash = 100.00m,
            Card = 50.00m,
            Voucher = 25.00m,
            Other = 0m,
            Total = 175.00m,
        };
}
