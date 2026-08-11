using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.DTOs;

/// <summary>Aggregated backup monitoring metrics for admin dashboard (30-day window).</summary>
public sealed class BackupDashboardStatsResponseDto
{
    public DateTime? LastBackupAtUtc { get; init; }

    public BackupRunStatus? LastBackupStatus { get; init; }

    public Guid? LastBackupRunId { get; init; }

    public DateTime? LastSuccessfulBackupAtUtc { get; init; }

    public long? BackupSizeBytes { get; init; }

    /// <summary>Terminal runs in last 30 days; null when no terminal runs in window.</summary>
    public double? SuccessRate30DaysPercent { get; init; }

    /// <summary>Current 30d rate minus prior 30d rate (percentage points); null when not comparable.</summary>
    public double? SuccessRateTrendVsPrior30DaysPercent { get; init; }

    public int TerminalRuns30Days { get; init; }

    public int SucceededRuns30Days { get; init; }

    /// <summary>Failed + VerificationFailed in last 30 days.</summary>
    public int FailedRuns30Days { get; init; }

    /// <summary>Queued + Running (active) runs visible to the caller.</summary>
    public int PendingRunsCount { get; init; }

    /// <summary>Total runs in the 30-day window (all statuses).</summary>
    public int TotalRuns30Days { get; init; }

    /// <summary>Earliest next scheduled fire (UTC) from enabled schedule rows; null when none.</summary>
    public DateTime? NextScheduledBackupAtUtc { get; init; }

    /// <summary>Staging volume used percent when measurable; null when unset/unavailable.</summary>
    public double? StagingDiskUsedPercent { get; init; }

    /// <summary>True when staging usage is at/above <c>Backup:StagingDiskUsageAlertPercent</c>.</summary>
    public bool StagingDiskAlert { get; init; }

    /// <summary>Hours since last succeeded backup; null when none.</summary>
    public double? RpoHours { get; init; }

    /// <summary>Average restore drill duration (minutes) or backup duration fallback.</summary>
    public double? RtoMinutes { get; init; }

    public DateTime? LastSuccessfulRestoreDrillAtUtc { get; init; }

    public RestoreVerificationStatus? LatestRestoreDrillStatus { get; init; }

    public DateTime? LastVerifiedBackupAtUtc { get; init; }

    /// <summary>Latest accessible verification row status (Passed/Failed/Pending); null when none.</summary>
    public BackupVerificationStatus? LastVerificationStatus { get; init; }

    public Guid? LastVerificationRunId { get; init; }

    /// <summary>Healthy | AtRisk | Critical | Unknown — derived from RPO vs AlertOnNoBackupDays.</summary>
    public string RpoStatus { get; init; } = "Unknown";

    /// <summary>0–100 composite health score (config + RPO + verification + drill).</summary>
    public int HealthScore { get; init; }

    /// <summary>healthy (80–100) | warning (50–79) | critical (0–49)</summary>
    public string HealthLevel { get; init; } = "warning";

    /// <summary>Lightweight content-validation summary for dashboard (not a full report).</summary>
    public BackupDashboardContentValidationSummaryDto? ContentValidationSummary { get; init; }

    public double? AverageSucceededBackupDurationSeconds { get; init; }

    public int AverageSucceededBackupDurationSampleCount { get; init; }

    public BackupConfigurationHealthResponseDto ConfigurationHealth { get; init; } = null!;

    public BackupArtifactPipelinePolicyResponseDto ArtifactPipelinePolicy { get; init; } = null!;

    public IReadOnlyList<BackupDashboardHistoryPointDto> History30Days { get; init; } =
        Array.Empty<BackupDashboardHistoryPointDto>();
}

/// <summary>
/// Aggregated backup health projection for widgets (from dashboard/stats; not a parallel computation path).
/// </summary>
public sealed class BackupDashboardHealthResponseDto
{
    public int HealthScore { get; init; }

    /// <summary>healthy | warning | critical</summary>
    public string HealthLevel { get; init; } = "warning";

    /// <summary>Passed | Failed | None</summary>
    public string VerificationStatus { get; init; } = "None";

    public Guid? LastVerificationRunId { get; init; }

    /// <summary>passed | failed | partial | unavailable | unknown</summary>
    public string ContentValidationStatus { get; init; } = "unknown";

    public string? ContentValidationSummary { get; init; }

    /// <summary>Healthy | AtRisk | Critical | Unknown</summary>
    public string RpoStatus { get; init; } = "Unknown";

    public double? RpoHours { get; init; }

    public DateTime? LastSuccessfulBackupAtUtc { get; init; }
}

public sealed class BackupDashboardContentValidationSummaryDto
{
    /// <summary>passed | failed | partial | available | unavailable | unknown</summary>
    public string Status { get; init; } = "unknown";

    public string? Summary { get; init; }

    public Guid? LastSucceededRunId { get; init; }
}

public sealed class BackupDashboardHistoryPointDto
{
    public Guid RunId { get; init; }

    public DateTime CompletedAtUtc { get; init; }

    public BackupRunStatus Status { get; init; }

    /// <summary>1 when <see cref="Status"/> is Succeeded; otherwise 0.</summary>
    public int Success { get; init; }

    /// <summary>1 when terminal failure (Failed or VerificationFailed); otherwise 0.</summary>
    public int Failed { get; init; }

    public double DurationSeconds { get; init; }
}
