namespace KasseAPI_Final.DTOs;

public sealed class ActivityReportDto
{
    public Guid TenantId { get; set; }
    public ActivityReportDateRangeDto Period { get; set; } = new();
    public int TotalActivities { get; set; }
    public int UniqueUsers { get; set; }
    public IReadOnlyList<ActivitySummaryDto> ActivitySummary { get; set; } = Array.Empty<ActivitySummaryDto>();
    public IReadOnlyList<ActivityAnomalyDto> Anomalies { get; set; } = Array.Empty<ActivityAnomalyDto>();
    public IReadOnlyList<string> Recommendations { get; set; } = Array.Empty<string>();
}

public sealed class ActivityReportDateRangeDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
}

public sealed class ActivitySummaryDto
{
    public string OperationType { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Users { get; set; }
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
}

public sealed class ActivityAnomalyDto
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? OperationType { get; set; }
    public string Severity { get; set; } = "Medium";
}
