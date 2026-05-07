using Cronos;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Tracks last scheduled success and applies DB-driven retention.</summary>
public sealed class BackupPostSuccessOrchestrationHook : IBackupPostSuccessOrchestrationHook
{
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<BackupPostSuccessOrchestrationHook> _logger;

    public BackupPostSuccessOrchestrationHook(
        IOptionsMonitor<BackupOptions> backupOptions,
        IHostEnvironment hostEnvironment,
        ILogger<BackupPostSuccessOrchestrationHook> logger)
    {
        _backupOptions = backupOptions;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task NotifySucceededAsync(AppDbContext db, BackupRun run, CancellationToken cancellationToken = default)
    {
        await BackupSettingsEnsure.EnsureSingletonAsync(db, cancellationToken);
        var settings = await db.BackupSettings.FirstAsync(x => x.Id == BackupSettings.SingletonId, cancellationToken);
        var utcNow = DateTime.UtcNow;

        if (run.TriggerSource == BackupTriggerSource.Scheduled)
        {
            settings.LastRunAt = run.CompletedAt ?? utcNow;
            RefreshNextRunAt(settings, utcNow);
            settings.UpdatedAtUtc = utcNow;
            _logger.LogInformation(
                "Backup automation: scheduled run succeeded — updated LastRunAt. runId={RunId}, completedAt={Completed:o}",
                run.Id,
                run.CompletedAt);
            await db.SaveChangesAsync(cancellationToken);
        }

        try
        {
            _logger.LogInformation(
                "Backup retention: evaluating succeeded runs older than {RetentionDays} day(s)",
                settings.RetentionDays);
            var removed = await BackupSucceededRunRetentionCleaner.DeleteExpiredSucceededRunsAsync(
                db,
                _backupOptions.CurrentValue,
                _hostEnvironment,
                _logger,
                settings.RetentionDays,
                cancellationToken);
            if (removed > 0)
            {
                _logger.LogInformation("Backup retention staged removal of {Removed} expired succeeded run row(s)", removed);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Backup retention pass failed after successful backup.");
        }
    }

    private static void RefreshNextRunAt(BackupSettings settings, DateTime utcNow)
    {
        if (!settings.Enabled ||
            string.IsNullOrWhiteSpace(settings.ScheduleCron) ||
            !CronExpression.TryParse(settings.ScheduleCron.Trim(), CronFormat.Standard, out var expr))
        {
            settings.NextRunAt = null;
            return;
        }

        settings.NextRunAt = expr.GetNextOccurrence(utcNow, TimeZoneInfo.Utc, inclusive: false);
    }
}
