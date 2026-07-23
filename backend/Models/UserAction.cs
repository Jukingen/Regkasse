namespace KasseAPI_Final.Models;

/// <summary>
/// Input snapshot for <see cref="Services.RiskScoring.IRiskScoringService.CalculateRisk"/>.
/// Not an EF entity — built by callers from audit/activity context.
/// </summary>
public sealed class UserAction
{
    public Guid TenantId { get; set; }

    /// <summary>AspNetUsers Id.</summary>
    public string UserId { get; set; } = string.Empty;

    public string ActionType { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Items affected by this bulk operation (0 when not bulk).</summary>
    public int BulkCount { get; set; }

    /// <summary>Historical average bulk size for this action type (tenant/user).</summary>
    public double AverageBulkCount { get; set; }

    public bool IsKnownIp { get; set; } = true;

    public bool IsRapidSuccession { get; set; }

    public bool IsFirstTime { get; set; }

    public string? IpAddress { get; set; }

    public string? CorrelationId { get; set; }
}
