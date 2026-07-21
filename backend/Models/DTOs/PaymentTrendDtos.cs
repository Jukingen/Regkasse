using System.Text.Json.Serialization;

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
    public TrendPeriod Period { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public IReadOnlyList<TrendDataPoint> TrendData { get; init; } = [];
    public ComparisonData Comparison { get; init; } = new();
    public TrendSummary Summary { get; init; } = new();
}

public sealed class TrendDataPoint
{
    public DateTime Date { get; init; }
    public decimal TotalAmount { get; init; }
    public int TransactionCount { get; init; }
    public decimal AverageAmount { get; init; }

    /// <summary>ISO week when <see cref="TrendPeriod.Weekly"/>; prefer <see cref="Label"/> / <see cref="Date"/> in clients.</summary>
    [Obsolete("Not consumed by FA charts; use label/date. Planned removal after 2026-12-31.")]
    [JsonPropertyName("weekNumber")]
    public int? WeekNumber { get; init; }

    public string? Label { get; init; }
}

public sealed class ComparisonData
{
    public decimal PreviousPeriodTotal { get; init; }
    public decimal CurrentPeriodTotal { get; init; }
    public decimal GrowthPercentage { get; init; }
    public string Trend { get; init; } = "stable";
    public IReadOnlyList<PaymentMethodComparison> PaymentMethodComparison { get; init; } = [];
}

public sealed class PaymentMethodComparison
{
    public string Method { get; init; } = string.Empty;
    public decimal CurrentAmount { get; init; }
    public decimal PreviousAmount { get; init; }
    public decimal ChangePercentage { get; init; }
}

public sealed class TrendSummary
{
    public decimal TotalRevenue { get; init; }
    public int TotalTransactions { get; init; }
    public decimal AverageTransactionValue { get; init; }
    public string? BestDay { get; init; }
    public decimal BestDayRevenue { get; init; }
    public string? MostUsedPaymentMethod { get; init; }
    public decimal PeakHourRevenue { get; init; }
    public int PeakHour { get; init; }
}
