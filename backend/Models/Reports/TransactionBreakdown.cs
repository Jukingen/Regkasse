namespace KasseAPI_Final.Models.Reports;

/// <summary>Receipt counts by payment type for daily closing reports.</summary>
public sealed class TransactionBreakdown
{
    public int Cash { get; set; }

    public int Card { get; set; }

    public int Voucher { get; set; }

    public int Cancellations { get; set; }

    public int Total { get; set; }
}
