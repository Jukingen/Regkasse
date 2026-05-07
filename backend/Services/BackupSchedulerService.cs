using Cronos;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Database-driven scheduled backup enqueue (UTC cron stored in backup_settings).
/// Executes under the same PostgreSQL advisory lock as <see cref="BackupOrchestratorHostedService"/> to avoid concurrent enqueue/dequeue conflicts.
/// </summary>
public sealed class BackupSchedulerService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackupOrchestratorDistributedLock _distributedLock;
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly ILogger<BackupSchedulerService> _logger;

    public BackupSchedulerService(
        IServiceScopeFactory scopeFactory,
        IBackupOrchestratorDistributedLock distributedLock,
        IOptionsMonitor<BackupOptions> backupOptions,
        ILogger<BackupSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _distributedLock = distributedLock;
        _backupOptions = backupOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_backupOptions.CurrentValue.WorkerEnabled)
                    await TryEnqueueScheduledIfDueExclusiveAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BackupSchedulerService tick failed.");
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task TryEnqueueScheduledIfDueExclusiveAsync(CancellationToken ct)
    {
        var (attempt, lease) = await _distributedLock.TryEnterExclusiveAsync(ct);
        IAsyncDisposable? d = lease;
        try
        {
            if (attempt == BackupOrchestratorGateAttempt.ContendedElsewhere)
                return;

            if (attempt == BackupOrchestratorGateAttempt.ConnectionFailed)
            {
                _logger.LogWarning(
                    "BackupSchedulerService skipped: distributed orchestrator gate did not acquire lock (another instance owns it or DB error).");
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var enqueue = scope.ServiceProvider.GetRequiredService<IBackupScheduledEnqueueService>();

            var enqueued = await enqueue.TryEnqueueIfDueAsync(db, ct);
            if (enqueued)
                _logger.LogInformation("BackupSchedulerService enqueued a scheduled backup row.");

            await TryRefreshStoredNextProjectionAsync(db, ct);
        }
        finally
        {
            if (d != null)
                await d.DisposeAsync();
        }
    }

    private async Task TryRefreshStoredNextProjectionAsync(AppDbContext db, CancellationToken ct)
    {
        try
        {
            await BackupSettingsEnsure.EnsureSingletonAsync(db, ct);
            var settings = await db.BackupSettings.FirstAsync(x => x.Id == BackupSettings.SingletonId, ct);
            if (!settings.Enabled ||
                string.IsNullOrWhiteSpace(settings.ScheduleCron) ||
                !CronExpression.TryParse(settings.ScheduleCron.Trim(), CronFormat.Standard, out var cron))
            {
                if (settings.NextRunAt != null)
                {
                    settings.NextRunAt = null;
                    settings.UpdatedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("BackupSchedulerService cleared stored NextRunAt (automation disabled or invalid cron).");
                }
                return;
            }

            var utcNow = DateTime.UtcNow;
            var next = cron.GetNextOccurrence(utcNow, TimeZoneInfo.Utc, inclusive: false);
            if (settings.NextRunAt == next)
                return;

            settings.NextRunAt = next;
            settings.UpdatedAtUtc = utcNow;
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("BackupSchedulerService refreshed stored NextRunAt to {Next:o} (UTC).", next);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "BackupSchedulerService skipped refreshing NextRunAt projection.");
        }
    }
}
