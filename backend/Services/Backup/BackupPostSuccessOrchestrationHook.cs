using Cronos;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Tracks last scheduled success and applies strategy-aware retention
/// (flat Tenant ~30d / System ~90d, or GFS when <see cref="BackupOptions.SmartRetentionEnabled"/>)
/// plus optional Hot/Warm/Cold storage-tier classification.
/// </summary>
public sealed class BackupPostSuccessOrchestrationHook : IBackupPostSuccessOrchestrationHook
{
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ISmartRetentionService _smartRetention;
    private readonly IStorageTierService _storageTier;
    private readonly ILogger<BackupPostSuccessOrchestrationHook> _logger;

    public BackupPostSuccessOrchestrationHook(
        IOptionsMonitor<BackupOptions> backupOptions,
        IHostEnvironment hostEnvironment,
        ISmartRetentionService smartRetention,
        IStorageTierService storageTier,
        ILogger<BackupPostSuccessOrchestrationHook> logger)
    {
        _backupOptions = backupOptions;
        _hostEnvironment = hostEnvironment;
        _smartRetention = smartRetention;
        _storageTier = storageTier;
        _logger = logger;
    }

    public async Task NotifySucceededAsync(AppDbContext db, BackupRun run, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        if (run.TriggerSource == BackupTriggerSource.Scheduled)
        {
            var tenantRows = await db.BackupScheduleConfigurations
                .IgnoreQueryFilters()
                .Where(c => c.IsActive && c.Enabled)
                .ToListAsync(cancellationToken);

            if (tenantRows.Count > 0)
            {
                var completedAt = run.CompletedAt ?? utcNow;
                foreach (var row in tenantRows)
                {
                    row.LastRunAt = completedAt;
                    BackupScheduleProjectionHelper.RefreshNextRunAt(row, utcNow);
                    row.UpdatedAt = utcNow;
                }
            }
            else
            {
                await BackupSettingsEnsure.EnsureSingletonAsync(db, cancellationToken);
                var settings = await db.BackupSettings.FirstAsync(x => x.Id == BackupSettings.SingletonId, cancellationToken);
                settings.LastRunAt = run.CompletedAt ?? utcNow;
                if (settings.Enabled
                    && !string.IsNullOrWhiteSpace(settings.ScheduleCron)
                    && CronExpression.TryParse(settings.ScheduleCron.Trim(), CronFormat.Standard, out var expr))
                    settings.NextRunAt = expr.GetNextOccurrence(utcNow, TimeZoneInfo.Utc, inclusive: false);
                else
                    settings.NextRunAt = null;
                settings.UpdatedAtUtc = utcNow;
            }

            _logger.LogInformation(
                "Backup automation: scheduled run succeeded — updated LastRunAt. runId={RunId}, completedAt={Completed:o}",
                run.Id,
                run.CompletedAt);
            await db.SaveChangesAsync(cancellationToken);
        }

        try
        {
            var tenantRetention = await ResolveTenantRetentionDaysAsync(db, cancellationToken);
            var systemRetention = await ResolveSystemRetentionDaysAsync(db, cancellationToken);
            _logger.LogInformation(
                "Backup retention: tenantWindow={TenantDays}d, systemWindow={SystemDays}d",
                tenantRetention,
                systemRetention);
            var removed = await BackupSucceededRunRetentionCleaner.DeleteExpiredSucceededRunsAsync(
                db,
                _backupOptions.CurrentValue,
                _hostEnvironment,
                _logger,
                tenantRetentionDays: tenantRetention,
                systemRetentionDays: systemRetention,
                cancellationToken,
                _smartRetention);
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

        if (_backupOptions.CurrentValue.StorageTierManagementEnabled)
        {
            try
            {
                var tiered = await _storageTier.ApplyOptimalTiersForSucceededRunsAsync(db, cancellationToken);
                if (tiered > 0)
                {
                    _logger.LogInformation(
                        "Backup storage tier pass updated {Count} succeeded run(s)",
                        tiered);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Backup storage tier pass failed after successful backup.");
            }
        }
    }

    private static async Task<int> ResolveTenantRetentionDaysAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantMax = await db.BackupScheduleConfigurations
            .IgnoreQueryFilters()
            .Where(c => c.IsActive && c.Enabled)
            .Select(c => (int?)c.RetentionDays)
            .MaxAsync(cancellationToken);

        return tenantMax ?? BackupStrategyPolicy.TenantRetentionDays;
    }

    private static async Task<int> ResolveSystemRetentionDaysAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        await BackupSettingsEnsure.EnsureSingletonAsync(db, cancellationToken);
        var singleton = await db.BackupSettings.AsNoTracking()
            .FirstAsync(x => x.Id == BackupSettings.SingletonId, cancellationToken);
        return Math.Max(singleton.RetentionDays, BackupStrategyPolicy.SystemRetentionDays);
    }
}
