namespace KasseAPI_Final.Models.DTOs;

/// <summary>Compliance-oriented activity summary for a single user (RKSV audit trail).</summary>
public sealed class UserActivityReportDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;

    public DateTime FromDateUtc { get; set; }
    public DateTime ToDateUtc { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public int TotalLogins { get; set; }
    public int FailedLoginAttempts { get; set; }

    public int ActiveSessions { get; set; }
    public double AverageSessionDurationMinutes { get; set; }
    public DateTime? LastSessionEndAt { get; set; }

    /// <summary>All audit events by this user in the selected period.</summary>
    public int TotalActions { get; set; }

    public UserActivityActionSummaryDto ActionsPerformed { get; set; } = new();
    public List<UserActivityDailyCountDto> DailyActivity { get; set; } = new();
    public List<UserActivityRankingDto> TopActiveUsers { get; set; } = new();
    public List<UserActivityTimelineItemDto> ActivityTimeline { get; set; } = new();
}

public sealed class UserActivityActionSummaryDto
{
    public int UserCreates { get; set; }
    public int UserEdits { get; set; }
    public int PaymentsProcessed { get; set; }
    public int Stornos { get; set; }
    public int Refunds { get; set; }
    public int Exports { get; set; }
}

public sealed class UserActivityDailyCountDto
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public sealed class UserActivityRankingDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int ActionCount { get; set; }
}

/// <summary>RKSV-relevant audit row for regulatory review.</summary>
public sealed class UserActivityTimelineItemDto
{
    public DateTime Date { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? CorrelationId { get; set; }
    public string? Description { get; set; }
    public string? TseSignature { get; set; }
}

public sealed class UserActivityReportQuery
{
    public string UserId { get; set; } = string.Empty;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? ActionType { get; set; }
    public bool IncludeTimeline { get; set; } = true;
    public bool IncludeTopUsers { get; set; } = true;
    public int TimelineLimit { get; set; } = 100;
    public int DefaultRangeDays { get; set; } = 30;
}
