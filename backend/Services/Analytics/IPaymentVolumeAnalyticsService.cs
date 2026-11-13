namespace KasseAPI_Final.Services.Analytics;

public sealed record DailyVolumeDto(DateTime Date, decimal Revenue, int TransactionCount);

public sealed record MonthlyVolumeDto(string YearMonth, decimal Revenue, int TransactionCount);

/// <summary>
/// Super Admin POS payment-volume snapshot (fiscal <c>payment_details</c> GMV).
/// Not license MRR / not digital-subscription revenue.
/// </summary>
public sealed record PaymentVolumeAnalyticsDto(
    decimal TotalRevenue,
    decimal RevenueThisMonth,
    decimal RevenueLastMonth,
    decimal MonthlyGrowth,
    int TotalTransactions,
    int TransactionsThisMonth,
    int TransactionsLastMonth,
    decimal AverageTransactionValue,
    IReadOnlyList<DailyVolumeDto> DailyVolume,
    IReadOnlyList<MonthlyVolumeDto> MonthlyVolume);

public interface IPaymentVolumeAnalyticsService
{
    Task<PaymentVolumeAnalyticsDto> GetPaymentVolumeAnalyticsAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string groupBy = "month",
        CancellationToken cancellationToken = default);
}
