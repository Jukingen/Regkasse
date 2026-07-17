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

    public double? AverageSucceededBackupDurationSeconds { get; init; }

    public int AverageSucceededBackupDurationSampleCount { get; init; }

    public BackupConfigurationHealthResponseDto ConfigurationHealth { get; init; } = null!;

    public BackupArtifactPipelinePolicyResponseDto ArtifactPipelinePolicy { get; init; } = null!;

    public IReadOnlyList<BackupDashboardHistoryPointDto> History30Days { get; init; } =
        Array.Empty<BackupDashboardHistoryPointDto>();
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
