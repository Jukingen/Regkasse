namespace KasseAPI_Final.DTOs;

/// <summary>Heuristic TSE failure-risk prediction (not a certified ML model).</summary>
public sealed class TsePredictionResultDto
{
    public Guid DeviceId { get; set; }
    public string? DeviceLabel { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime GeneratedAt { get; set; }

    /// <summary>0–100 estimated probability of operational failure in the near term.</summary>
    public double FailureProbability { get; set; }

    /// <summary>Low | Medium | High | Critical</summary>
    public string RiskLevel { get; set; } = TsePredictiveRiskLevels.Low;

    public DateTime? PredictedFailureDate { get; set; }
    public int CurrentHealthScore { get; set; }
    public string CurrentHealthStatus { get; set; } = "Healthy";
    public double HealthTrendPerDay { get; set; }
    public int SampleCount { get; set; }

    public IReadOnlyList<TseRiskFactorDto> RiskFactors { get; set; } = Array.Empty<TseRiskFactorDto>();
    public IReadOnlyList<string> Recommendations { get; set; } = Array.Empty<string>();
    public bool RequiresImmediateAction { get; set; }
    public bool AlertPublished { get; set; }
}

public sealed class TseRiskFactorDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>0–100 contribution toward failure risk.</summary>
    public double Impact { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActionable { get; set; }
    public string? RecommendedAction { get; set; }
    public Guid? DeviceId { get; set; }
}

public sealed class TseHealthPredictionDto
{
    public Guid DeviceId { get; set; }
    public string? DeviceLabel { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int ForecastDays { get; set; }
    public int CurrentHealthScore { get; set; }
    public double HealthTrendPerDay { get; set; }
    public int PredictedHealthScoreAtHorizon { get; set; }
    public string PredictedRiskLevel { get; set; } = TsePredictiveRiskLevels.Low;
    public DateTime? PredictedBreachDate { get; set; }
    public int HealthyMinScore { get; set; }
    public int DegradedMinScore { get; set; }
    public IReadOnlyList<TseHealthForecastPointDto> ForecastPoints { get; set; } =
        Array.Empty<TseHealthForecastPointDto>();
}

public sealed class TseHealthForecastPointDto
{
    public DateTime Date { get; set; }
    public int PredictedScore { get; set; }
    public string PredictedStatus { get; set; } = "Healthy";
}

public static class TsePredictiveRiskLevels
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";
}
