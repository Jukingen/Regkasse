using Cronos;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.OperationalRuns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Manuel API kullanmadan <see cref="BackupTriggerSource.Scheduled"/> satırları üretir; aktif zamanlanmış yedek varken çift sıraya almaz.
/// </summary>
public sealed class BackupScheduledEnqueueService : IBackupScheduledEnqueueService
{
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IBackupOperationalReadiness _readiness;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BackupScheduledEnqueueService> _logger;

    public BackupScheduledEnqueueService(
        IOptionsMonitor<BackupOptions> options,
        IBackupOperationalReadiness readiness,
        TimeProvider timeProvider,
        ILogger<BackupScheduledEnqueueService> logger)
    {
        _options = options;
        _readiness = readiness;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> TryEnqueueIfDueAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.ScheduledBackupEnabled || !opts.WorkerEnabled)
            return false;

        var health = _readiness.GetConfigurationHealth();
        if (health.Level == BackupConfigurationHealthLevel.Unhealthy)
        {
            _logger.LogWarning(
                "Scheduled backup enqueue skipped: backup configuration health is Unhealthy.");
            return false;
        }

        var cronText = opts.GetEffectiveScheduledBackupCronExpression();
        if (string.IsNullOrWhiteSpace(cronText))
            return false;

        if (!CronExpression.TryParse(cronText, CronFormat.Standard, out var expr))
        {
            _logger.LogWarning("Scheduled backup enqueue skipped: cron expression failed to parse at runtime.");
            return false;
        }

        var utcNow = DateTime.SpecifyKind(_timeProvider.GetUtcNow().UtcDateTime, DateTimeKind.Utc);

        var hasActiveScheduled = await db.BackupRuns.AsNoTracking()
            .AnyAsync(
                r => r.TriggerSource == BackupTriggerSource.Scheduled
                     && (r.Status == BackupRunStatus.Queued
                         || r.Status == BackupRunStatus.Running
                         || r.Status == BackupRunStatus.AwaitingVerification),
                cancellationToken);
        if (hasActiveScheduled)
            return false;

        var lastScheduledRequestedAt = await db.BackupRuns.AsNoTracking()
            .Where(r => r.TriggerSource == BackupTriggerSource.Scheduled)
            .Select(r => (DateTime?)r.RequestedAt)
            .MaxAsync(cancellationToken);

        var anchor = lastScheduledRequestedAt ?? utcNow.AddYears(-10);
        var nextFire = expr.GetNextOccurrence(anchor, TimeZoneInfo.Utc, inclusive: false);
        if (nextFire == null || nextFire.Value > utcNow)
            return false;

        var effectiveKind = health.EffectiveAdapterKind;
        var adapterKind = effectiveKind.ToString();
        var capturedAt = utcNow;
        var run = new BackupRun
        {
            Status = BackupRunStatus.Queued,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = adapterKind,
            RequestedAt = capturedAt,
            QueuedAt = capturedAt,
            CorrelationId = null,
            ConfigSnapshotJson = OperationalRunConfigSnapshotBuilder.SerializeBackup(
                opts,
                "backup_scheduled_enqueue",
                capturedAt,
                effectiveKind,
                health.AdminRuntimeExecutionMode)
        };
        run.CorrelationId = $"sched-{run.Id:N}";

        db.BackupRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Enqueued scheduled backup run: runId={RunId}, adapterKind={AdapterKind}",
            run.Id,
            adapterKind);

        return true;
    }
}
