using System.Globalization;
using System.Text.Json.Serialization;
using KasseAPI_Final.Time;

namespace KasseAPI_Final.Models.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TrendPeriod
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
}

public sealed class TrendAnalysisResponse
{
    public TrendPeriod Period { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public IReadOnlyList<TrendDataPoint> TrendData { get; set; } = [];
    public ComparisonData Comparison { get; set; } = new();
    public TrendSummary Summary { get; set; } = new();
}

public sealed class TrendDataPoint
{
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageAmount { get; set; }
    public int? WeekNumber { get; set; }
    public string? Label { get; set; }
}

public sealed class ComparisonData
{
    public decimal PreviousPeriodTotal { get; set; }
    public decimal CurrentPeriodTotal { get; set; }
    public decimal GrowthPercentage { get; set; }
    public string Trend { get; set; } = "stable";
    public IReadOnlyList<PaymentMethodComparison> PaymentMethodComparison { get; set; } = [];
}

public sealed class PaymentMethodComparison
{
    public string Method { get; set; } = string.Empty;
    public decimal CurrentAmount { get; set; }
    public decimal PreviousAmount { get; set; }
    public decimal ChangePercentage { get; set; }
}

public sealed class TrendSummary
{
    public decimal TotalRevenue { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageTransactionValue { get; set; }
    public string? BestDay { get; set; }
    public decimal BestDayRevenue { get; set; }
    public string? MostUsedPaymentMethod { get; set; }
    public decimal PeakHourRevenue { get; set; }
    public int PeakHour { get; set; }
}
