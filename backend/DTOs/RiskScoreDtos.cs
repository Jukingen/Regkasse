using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class RiskScoreDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? UserName { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int Score { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? Resolution { get; set; }
}

public sealed class RiskScoreListResponseDto
{
    public int Total { get; set; }
    public IReadOnlyList<RiskScoreDto> Items { get; set; } = Array.Empty<RiskScoreDto>();
    public RiskScoreSummaryDto Summary { get; set; } = new();
}

public sealed class RiskScoreSummaryDto
{
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public int Low { get; set; }
    public int Open { get; set; }
}

public sealed class ResolveRiskScoreRequestDto
{
    [Required]
    [MaxLength(2000)]
    public string Resolution { get; set; } = string.Empty;
}

public sealed class EvaluateUserActionRequestDto
{
    [Required]
    [MaxLength(128)]
    public string ActionType { get; set; } = string.Empty;

    public DateTime? Timestamp { get; set; }

    [Range(0, int.MaxValue)]
    public int BulkCount { get; set; }

    [Range(0, double.MaxValue)]
    public double AverageBulkCount { get; set; }

    public bool IsKnownIp { get; set; } = true;

    public bool IsRapidSuccession { get; set; }

    public bool IsFirstTime { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// When true (default), persist the score when level is Medium or above.
    /// </summary>
    public bool PersistIfElevated { get; set; } = true;
}

public sealed class EvaluateUserActionResponseDto
{
    public int Score { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Guid? PersistedId { get; set; }
}
