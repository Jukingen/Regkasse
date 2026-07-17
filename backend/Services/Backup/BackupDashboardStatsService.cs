using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Backup monitoring dashboard aggregation (30-day runs, RPO/RTO, configuration health).
/// </summary>
public sealed class BackupDashboardStatsService : IBackupDashboardStatsService
{
    private static readonly BackupRunStatus[] TerminalStatuses =
    {
        BackupRunStatus.Succeeded,
        BackupRunStatus.Failed,
        BackupRunStatus.VerificationFailed,
        BackupRunStatus.Cancelled,
    };

    private readonly AppDbContext _db;
    private readonly IBackupRunQueryService _runQuery;
    private readonly IBackupOperationalReadiness _readiness;
    private readonly IBackupStagingDiskMonitor _diskMonitor;
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly TimeProvider _timeProvider;

    public BackupDashboardStatsService(
        AppDbContext db,
        IBackupRunQueryService runQuery,
        IBackupOperationalReadiness readiness,
        IBackupStagingDiskMonitor diskMonitor,
        IOptionsMonitor<BackupOptions> backupOptions,
        TimeProvider timeProvider)
    {
        _db = db;
        _runQuery = runQuery;
        _readiness = readiness;
        _diskMonitor = diskMonitor;
        _backupOptions = backupOptions;
        _timeProvider = timeProvider;
    }

    public async Task<BackupDashboardStatsResponseDto> GetAsync(
        BackupRunAccessScope? accessScope = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var windowStart = nowUtc.AddDays(-30);
        var priorWindowStart = nowUtc.AddDays(-60);

        var runs30 = await AccessibleRuns(accessScope)
            .Include(r => r.Artifacts)
            .Where(r => r.RequestedAt >= windowStart)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);

        var runsPrior30 = await AccessibleRuns(accessScope)
            .Where(r => r.RequestedAt >= priorWindowStart && r.RequestedAt < windowStart)
            .Select(r => new { r.Status, r.CompletedAt, r.RequestedAt })
            .ToListAsync(cancellationToken);

        var latestRun = runs30.OrderByDescending(r => r.RequestedAt).FirstOrDefault()
            ?? await AccessibleRuns(accessScope)
                .OrderByDescending(r => r.RequestedAt)
                .FirstOrDefaultAsync(cancellationToken);

        var lastSuccess = runs30
            .Where(r => r.Status == BackupRunStatus.Succeeded)
            .OrderByDescending(r => r.CompletedAt ?? r.RequestedAt)
            .FirstOrDefault();

        if (lastSuccess == null)
        {
            lastSuccess = await AccessibleRuns(accessScope)
                .Include(r => r.Artifacts)
                .Where(r => r.Status == BackupRunStatus.Succeeded)
                .OrderByDescending(r => r.CompletedAt ?? r.RequestedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var terminal30 = runs30.Where(r => TerminalStatuses.Contains(r.Status)).ToList();
        var succeeded30 = terminal30.Count(r => r.Status == BackupRunStatus.Succeeded);
        double? successRate = terminal30.Count > 0
            ? Math.Round((double)succeeded30 / terminal30.Count * 100.0, 1)
            : null;

        var terminalPrior = runsPrior30.Where(r => TerminalStatuses.Contains(r.Status)).ToList();
        var succeededPrior = terminalPrior.Count(r => r.Status == BackupRunStatus.Succeeded);
        double? trend = null;
        if (terminal30.Count >= 1 && terminalPrior.Count >= 1 && successRate.HasValue)
        {
            var priorRate = Math.Round((double)succeededPrior / terminalPrior.Count * 100.0, 1);
            trend = Math.Round(successRate.Value - priorRate, 1);
        }

        double? rpoHours = null;
        if (lastSuccess != null)
        {
            var proofAt = lastSuccess.CompletedAt ?? lastSuccess.RequestedAt;
            rpoHours = Math.Round((nowUtc - proofAt).TotalHours, 2);
            if (rpoHours < 0) rpoHours = 0;
        }

        var restoreDrills = await _db.RestoreVerificationRuns.AsNoTracking()
            .Where(r => r.Status == RestoreVerificationStatus.Succeeded
                        && r.StartedAt != null
                        && r.CompletedAt != null)
            .OrderByDescending(r => r.CompletedAt)
            .Take(10)
            .Select(r => new { r.StartedAt, r.CompletedAt })
            .ToListAsync(cancellationToken);

        var durationStats = await _runQuery.GetAverageSucceededDurationAsync(15, accessScope, cancellationToken);

        double? rtoMinutes = null;
        if (restoreDrills.Count > 0)
        {
            rtoMinutes = Math.Round(
                restoreDrills.Average(r => (r.CompletedAt!.Value - r.StartedAt!.Value).TotalMinutes),
                1);
        }
        else if (durationStats.AverageDurationSeconds.HasValue)
        {
            rtoMinutes = Math.Round(durationStats.AverageDurationSeconds.Value / 60.0, 1);
        }

        var lastRestoreProof = await _db.RestoreVerificationRuns.AsNoTracking()
            .Where(r => r.Status == RestoreVerificationStatus.Succeeded && r.CompletedAt != null)
            .OrderByDescending(r => r.CompletedAt)
            .Select(r => new { r.CompletedAt })
            .FirstOrDefaultAsync(cancellationToken);

        var latestRestore = await _db.RestoreVerificationRuns.AsNoTracking()
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new { r.Status })
            .FirstOrDefaultAsync(cancellationToken);

        var lastVerificationAt = await LastPassedVerificationAtAsync(accessScope, cancellationToken);

        var cfg = _readiness.GetConfigurationHealth();
        var artifactPolicy = _readiness.GetArtifactPipelinePolicy();

        var history = terminal30
            .Select(r => MapHistoryPoint(r))
            .OrderBy(p => p.CompletedAtUtc)
            .ToList();

        long? sizeBytes = lastSuccess == null
            ? null
            : lastSuccess.Artifacts
                .Where(a => a.ArtifactType == BackupArtifactType.LogicalDump)
                .Sum(a => a.ByteSize ?? 0L);

        var failed30 = terminal30.Count(r =>
            r.Status is BackupRunStatus.Failed or BackupRunStatus.VerificationFailed);
        var pendingCount = await AccessibleRuns(accessScope)
            .CountAsync(
                r => r.Status == BackupRunStatus.Queued || r.Status == BackupRunStatus.Running,
                cancellationToken);

        var nextScheduled = await ResolveNextScheduledBackupAtUtcAsync(cancellationToken);

        var opts = _backupOptions.CurrentValue;
        var disk = _diskMonitor.TryGetUsage(opts.ArtifactStagingRoot, opts.StagingDiskUsageAlertPercent);

        return new BackupDashboardStatsResponseDto
        {
            LastBackupAtUtc = latestRun?.CompletedAt ?? latestRun?.RequestedAt,
            LastBackupStatus = latestRun?.Status,
            LastBackupRunId = latestRun?.Id,
            LastSuccessfulBackupAtUtc = lastSuccess == null
                ? null
                : lastSuccess.CompletedAt ?? lastSuccess.RequestedAt,
            BackupSizeBytes = sizeBytes is > 0 ? sizeBytes : null,
            SuccessRate30DaysPercent = successRate,
            SuccessRateTrendVsPrior30DaysPercent = trend,
            TerminalRuns30Days = terminal30.Count,
            SucceededRuns30Days = succeeded30,
            FailedRuns30Days = failed30,
            PendingRunsCount = pendingCount,
            TotalRuns30Days = runs30.Count,
            NextScheduledBackupAtUtc = nextScheduled,
            StagingDiskUsedPercent = disk?.UsedPercent,
            StagingDiskAlert = disk?.Alert ?? false,
            RpoHours = rpoHours,
            RtoMinutes = rtoMinutes,
            LastSuccessfulRestoreDrillAtUtc = lastRestoreProof?.CompletedAt,
            LatestRestoreDrillStatus = latestRestore?.Status,
            LastVerifiedBackupAtUtc = lastVerificationAt ?? lastSuccess?.CompletedAt,
            AverageSucceededBackupDurationSeconds = durationStats.AverageDurationSeconds,
            AverageSucceededBackupDurationSampleCount = durationStats.SampleCount,
            ConfigurationHealth = BackupConfigurationHealthResponseMapper.FromSnapshot(cfg),
            ArtifactPipelinePolicy = BackupArtifactPipelinePolicyMapper.ToDto(artifactPolicy),
            History30Days = history,
        };
    }

    private async Task<DateTime?> ResolveNextScheduledBackupAtUtcAsync(CancellationToken cancellationToken)
    {
        var fromTenantSchedules = await _db.BackupScheduleConfigurations.AsNoTracking()
            .Where(c => c.Enabled && c.NextRunAt != null)
            .Select(c => c.NextRunAt)
            .ToListAsync(cancellationToken);

        var fromSingleton = await _db.BackupSettings.AsNoTracking()
            .Where(s => s.Enabled && s.NextRunAt != null)
            .Select(s => s.NextRunAt)
            .FirstOrDefaultAsync(cancellationToken);

        DateTime? earliest = null;
        foreach (var candidate in fromTenantSchedules)
        {
            if (candidate is not DateTime dt)
                continue;
            if (earliest == null || dt < earliest)
                earliest = dt;
        }

        if (fromSingleton is DateTime singletonNext
            && (earliest == null || singletonNext < earliest))
            earliest = singletonNext;

        return earliest;
    }

    private IQueryable<BackupRun> AccessibleRuns(BackupRunAccessScope? accessScope)
    {
        var q = _db.BackupRuns.AsNoTracking();
        return accessScope == null
            ? q
            : BackupRunAccessEvaluator.ApplyCallerAccessFilter(q, accessScope);
    }

    private async Task<DateTime?> LastPassedVerificationAtAsync(
        BackupRunAccessScope? accessScope,
        CancellationToken cancellationToken)
    {
        var accessibleRunIds = AccessibleRuns(accessScope);
        return await _db.BackupVerifications.AsNoTracking()
            .Where(v => v.Status == BackupVerificationStatus.Passed
                        && accessibleRunIds.Select(r => r.Id).Contains(v.BackupRunId))
            .OrderByDescending(v => v.CompletedAt ?? v.StartedAt)
            .Select(v => (DateTime?)(v.CompletedAt ?? v.StartedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static BackupDashboardHistoryPointDto MapHistoryPoint(BackupRun run)
    {
        var completed = run.CompletedAt ?? run.RequestedAt;
        var started = run.StartedAt ?? run.RequestedAt;
        var durationSec = run.CompletedAt.HasValue && run.StartedAt.HasValue
            ? Math.Max(0, (run.CompletedAt.Value - started).TotalSeconds)
            : 0;

        var failed = run.Status is BackupRunStatus.Failed or BackupRunStatus.VerificationFailed;

        return new BackupDashboardHistoryPointDto
        {
            RunId = run.Id,
            CompletedAtUtc = completed,
            Status = run.Status,
            Success = run.Status == BackupRunStatus.Succeeded ? 1 : 0,
            Failed = failed ? 1 : 0,
            DurationSeconds = Math.Round(durationSec, 1),
        };
    }
}
