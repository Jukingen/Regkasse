namespace KasseAPI_Final.DTOs;

public sealed class TseRecommendationDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    /// <summary>Indicative monthly EUR savings.</summary>
    public int EstimatedSavings { get; set; }
    /// <summary>1–10 effort score.</summary>
    public int EffortScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    public bool IsDismissed { get; set; }
    /// <summary>1–5; 0 = unrated.</summary>
    public int Rating { get; set; }
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseRecommendationResultDto
{
    public Guid RecommendationId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TseRecommendationDto? Recommendation { get; set; }
}

public sealed class TseRecommendationFeedbackDto
{
    public Guid RecommendationId { get; set; }
    public int Rating { get; set; }
    public DateTime RatedAt { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TseRecommendationDto? Recommendation { get; set; }
}

public sealed class TseRecommendationRateRequestDto
{
    public int Rating { get; set; }
}
