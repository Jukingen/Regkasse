namespace KasseAPI_Final.DTOs;

/// <summary>
/// Tenant-scoped TSE / POS user-behavior report (diagnostic UX analytics — not fiscal evidence).
/// </summary>
public sealed class TseUserBehaviorReportDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }

    // Engagement
    public int TotalSessions { get; set; }
    /// <summary>Average session length in minutes.</summary>
    public double AverageSessionDuration { get; set; }
    public int UniqueUsers { get; set; }
    public double DailyActiveUsers { get; set; }

    // Feature usage
    public Dictionary<string, int> FeatureUsage { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> FeatureAdoptionRate { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Drop-off / satisfaction
    public IReadOnlyList<TseDropoffPointDto> DropoffPoints { get; set; } =
        Array.Empty<TseDropoffPointDto>();
    public Dictionary<string, double> UserSatisfactionScores { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<TseFunnelStepDto> FunnelSteps { get; set; } =
        Array.Empty<TseFunnelStepDto>();

    public IReadOnlyList<TseUxRecommendationDto> Recommendations { get; set; } =
        Array.Empty<TseUxRecommendationDto>();

    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseDropoffPointDto
{
    public string FromStep { get; set; } = string.Empty;
    public string ToStep { get; set; } = string.Empty;
    public int FromCount { get; set; }
    public int ToCount { get; set; }
    /// <summary>0–100 percent of users who did not continue.</summary>
    public double DropoffPercent { get; set; }
    public string Severity { get; set; } = "Info";
}

public sealed class TseFunnelStepDto
{
    public string Step { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    /// <summary>Conversion vs first funnel step (0–100).</summary>
    public double ConversionPercent { get; set; }
}

public sealed class TseUxRecommendationDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string? RelatedFeature { get; set; }
}

/// <summary>Fleet or tenant feature-usage heatmap aggregates.</summary>
public sealed class TseFeatureUsageReportDto
{
    public Guid? TenantId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int UniqueUsers { get; set; }
    public Dictionary<string, int> FeatureUsage { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> FeatureAdoptionRate { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<TseFeatureHeatmapCellDto> Heatmap { get; set; } =
        Array.Empty<TseFeatureHeatmapCellDto>();
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseFeatureHeatmapCellDto
{
    public string Feature { get; set; } = string.Empty;
    public string DayOfWeek { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>Weekly signup-cohort retention for POS/admin sessions.</summary>
public sealed class TseCohortAnalysisResultDto
{
    public Guid? TenantId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int CohortWeeks { get; set; }
    public IReadOnlyList<TseCohortRowDto> Cohorts { get; set; } = Array.Empty<TseCohortRowDto>();
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseCohortRowDto
{
    public string CohortWeek { get; set; } = string.Empty;
    public DateTime CohortStart { get; set; }
    public int CohortSize { get; set; }
    /// <summary>Retention percent by relative week index (0 = signup week).</summary>
    public IReadOnlyList<double> RetentionByWeek { get; set; } = Array.Empty<double>();
}
