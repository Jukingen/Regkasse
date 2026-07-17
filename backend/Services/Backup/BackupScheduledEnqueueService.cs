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
/// UTC cron ile zamanlanmış yedek sıraya alma. Önce kiracı <see cref="BackupScheduleConfiguration"/> satırları;
/// hiçbiri etkin değilse singleton <c>backup_settings</c> veya yapılandırma cron'una düşer.
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
        if (!opts.WorkerEnabled)
            return false;

        var health = _readiness.GetConfigurationHealth();
        if (health.Level == BackupConfigurationHealthLevel.Unhealthy)
        {
            _logger.LogWarning(
                "Scheduled backup enqueue skipped: backup configuration health is Unhealthy.");
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

        var tenantConfigs = await db.BackupScheduleConfigurations
            .IgnoreQueryFilters()
            .Where(c => c.IsActive && c.Enabled)
            .ToListAsync(cancellationToken);

        var dueTenantConfigs = tenantConfigs
            .Where(c => BackupScheduleProjectionHelper.IsScheduleDue(c, utcNow))
            .ToList();

        var useTenantSchedules = dueTenantConfigs.Count > 0;
        string? cronText;
        var useDbSchedule = false;

        if (useTenantSchedules)
        {
            cronText = dueTenantConfigs[0].ScheduleCron.Trim();
        }
        else
        {
            await BackupSettingsEnsure.EnsureSingletonAsync(db, cancellationToken);
            var settingsRow = await db.BackupSettings.FirstAsync(x => x.Id == BackupSettings.SingletonId, cancellationToken);
            useDbSchedule = settingsRow.Enabled;
            cronText = useDbSchedule
                ? settingsRow.ScheduleCron?.Trim()
                : opts.GetEffectiveScheduledBackupCronExpression();

            if (string.IsNullOrWhiteSpace(cronText))
            {
                if (useDbSchedule)
                    _logger.LogDebug(
                        "Scheduled backup enqueue skipped: database automation enabled but schedule_cron empty.");
                return false;
            }

            if (!useDbSchedule && !opts.ScheduledBackupEnabled)
                return false;

            if (!CronExpression.TryParse(cronText, CronFormat.Standard, out var legacyExpr))
            {
                _logger.LogWarning(
                    "Scheduled backup enqueue skipped: cron expression failed to parse ({Source}).",
                    useDbSchedule ? "database" : "configuration");
                return false;
            }

            var lastScheduledRequestedAt = await BackupScheduleProjection.GetLastScheduledRequestedAtAsync(db, cancellationToken);
            var anchor = lastScheduledRequestedAt ?? utcNow.AddYears(-10);
            var nextFire = legacyExpr.GetNextOccurrence(anchor, TimeZoneInfo.Utc, inclusive: false);
            if (nextFire == null || nextFire.Value > utcNow)
                return false;
        }

        if (string.IsNullOrWhiteSpace(cronText)
            || !CronExpression.TryParse(cronText, CronFormat.Standard, out var expr))
        {
            _logger.LogWarning("Scheduled backup enqueue skipped: tenant cron failed to parse.");
            return false;
        }

        var effectiveKind = health.EffectiveAdapterKind;
        var adapterKind = effectiveKind.ToString();
        var capturedAt = utcNow;
        var run = new BackupRun
        {
            Status = BackupRunStatus.Queued,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = adapterKind,
            Strategy = BackupStrategyKind.System,
            TenantId = null,
            RequestedAt = capturedAt,
            QueuedAt = capturedAt,
            CorrelationId = null,
            ConfigSnapshotJson = OperationalRunConfigSnapshotBuilder.SerializeBackup(
                opts,
                useTenantSchedules ? "backup_scheduled_enqueue_tenant" : "backup_scheduled_enqueue",
                capturedAt,
                effectiveKind,
                health.AdminRuntimeExecutionMode,
                BackupStrategyKind.System)
        };

        db.BackupRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        run.CorrelationId = useTenantSchedules
            ? $"sched-tenant-{run.Id:N}"
            : $"sched-{run.Id:N}";

        if (useTenantSchedules)
        {
            foreach (var cfg in dueTenantConfigs)
            {
                BackupScheduleProjectionHelper.RefreshNextRunAt(cfg, capturedAt);
                cfg.UpdatedAt = capturedAt;
            }
        }
        else
        {
            var settingsRow = await db.BackupSettings.FirstAsync(x => x.Id == BackupSettings.SingletonId, cancellationToken);
            if (useDbSchedule)
            {
                settingsRow.NextRunAt = expr.GetNextOccurrence(capturedAt, TimeZoneInfo.Utc, inclusive: false);
                settingsRow.UpdatedAtUtc = capturedAt;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Enqueued scheduled backup run: runId={RunId}, adapterKind={AdapterKind}, scheduleSource={Source}, dueTenantCount={TenantCount}",
            run.Id,
            adapterKind,
            useTenantSchedules ? "tenant_schedules" : useDbSchedule ? "database" : "configuration",
            dueTenantConfigs.Count);

        return true;
    }
}
