namespace KasseAPI_Final.DTOs;

public sealed class FiskalyStatisticsQuery
{
    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public string? OperationType { get; init; }

    public Guid? TenantId { get; init; }
}

public sealed class FiskalyStatisticsKpisDto
{
    public int TotalOperations { get; init; }

    public double SuccessRatePercent { get; init; }

    public string? MostUsedOperationType { get; init; }

    public double? AverageProcessingTimeMs { get; init; }

    public int TotalErrors { get; init; }

    public int SuccessCount { get; init; }

    public int FailedCount { get; init; }

    public int InFlightCount { get; init; }
}

public sealed class FiskalyStatisticsCountDto
{
    public string Key { get; init; } = string.Empty;

    public int Count { get; init; }
}

public sealed class FiskalyStatisticsDailyPointDto
{
    public string Date { get; init; } = string.Empty;

    public int Total { get; init; }

    public int Success { get; init; }

    public int Failed { get; init; }
}

public sealed class FiskalyStatisticsMonthlyPointDto
{
    public string YearMonth { get; init; } = string.Empty;

    public int Total { get; init; }

    public int Success { get; init; }

    public int Failed { get; init; }
}

public sealed class FiskalyStatisticsDto
{
    public DateTime FromUtc { get; init; }

    public DateTime ToUtc { get; init; }

    public Guid? TenantId { get; init; }

    public FiskalyStatisticsKpisDto Kpis { get; init; } = new();

    public IReadOnlyList<FiskalyStatisticsDailyPointDto> Daily { get; init; } = [];

    public IReadOnlyList<FiskalyStatisticsCountDto> ByOperationType { get; init; } = [];

    public IReadOnlyList<FiskalyStatisticsCountDto> ByStatus { get; init; } = [];

    public IReadOnlyList<FiskalyStatisticsMonthlyPointDto> Monthly { get; init; } = [];
}
