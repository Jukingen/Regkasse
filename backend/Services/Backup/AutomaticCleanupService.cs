using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Daily (configurable) ops pass: smart/flat retention cleanup + optional Hot/Warm/Cold retag.
/// Uses <see cref="BackupRun"/> / <see cref="BackupArtifact"/> — not a fictional Backups table.
/// Complements post-success cleanup when no new succeeded runs enqueue retention.
/// </summary>
public sealed class AutomaticCleanupService : BackgroundService
{
    private static readonly TimeSpan StartupGrace = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MinInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ISmartRetentionService _smartRetention;
    private readonly IStorageTierService _storageTier;
    private readonly ILogger<AutomaticCleanupService> _logger;

    public AutomaticCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BackupOptions> options,
        IHostEnvironment hostEnvironment,
        ISmartRetentionService smartRetention,
        IStorageTierService storageTier,
        ILogger<AutomaticCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _hostEnvironment = hostEnvironment;
        _smartRetention = smartRetention;
        _storageTier = storageTier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupGrace, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;
            if (opts.AutomaticCleanupEnabled)
            {
                try
                {
                    await RunCleanupPassAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Automatic backup cleanup pass failed.");
                }
            }

            var delay = opts.AutomaticCleanupInterval;
            if (delay < MinInterval)
                delay = MinInterval;

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>One cleanup pass (retention delete + optional tier retag). Public for tests.</summary>
    public async Task<AutomaticCleanupPassResult> RunCleanupPassAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        var opts = _options.CurrentValue;

        var tenantRetention = await ResolveTenantRetentionDaysAsync(db, ct).ConfigureAwait(false);
        var systemRetention = await ResolveSystemRetentionDaysAsync(db, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Automatic backup cleanup starting: smartRetention={Smart}, storageTiers={Tiers}, tenantWindow={TenantDays}d, systemWindow={SystemDays}d",
            opts.SmartRetentionEnabled,
            opts.StorageTierManagementEnabled,
            tenantRetention,
            systemRetention);

        var removed = await BackupSucceededRunRetentionCleaner.DeleteExpiredSucceededRunsAsync(
            db,
            opts,
            _hostEnvironment,
            _logger,
            tenantRetentionDays: tenantRetention,
            systemRetentionDays: systemRetention,
            ct,
            _smartRetention).ConfigureAwait(false);

        if (removed > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.LogSystemOperationAsync(
                action: "BACKUP_AUTO_DELETED",
                entityType: "BackupRun",
                userId: "system",
                userRole: "System",
                description: $"Automatic cleanup removed {removed} succeeded backup run(s).",
                status: AuditLogStatus.Success,
                responseData: new
                {
                    removedCount = removed,
                    smartRetentionEnabled = opts.SmartRetentionEnabled,
                    tenantRetentionDays = tenantRetention,
                    systemRetentionDays = systemRetention,
                    atUtc = DateTime.UtcNow
                }).ConfigureAwait(false);

            _logger.LogInformation(
                "Automatic backup cleanup deleted {Removed} succeeded run(s)",
                removed);
        }

        var tiersUpdated = 0;
        if (opts.StorageTierManagementEnabled)
        {
            tiersUpdated = await _storageTier.ApplyOptimalTiersForSucceededRunsAsync(db, ct)
                .ConfigureAwait(false);
            if (tiersUpdated > 0)
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                _logger.LogInformation(
                    "Automatic backup cleanup retagged storage tiers on {Count} run(s)",
                    tiersUpdated);
            }
        }

        return new AutomaticCleanupPassResult(removed, tiersUpdated);
    }

    private static async Task<int> ResolveTenantRetentionDaysAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var tenantMax = await db.BackupScheduleConfigurations
            .IgnoreQueryFilters()
            .Where(c => c.IsActive && c.Enabled)
            .Select(c => (int?)c.RetentionDays)
            .MaxAsync(cancellationToken)
            .ConfigureAwait(false);

        return tenantMax ?? BackupStrategyPolicy.TenantRetentionDays;
    }

    private static async Task<int> ResolveSystemRetentionDaysAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        await BackupSettingsEnsure.EnsureSingletonAsync(db, cancellationToken).ConfigureAwait(false);
        var singleton = await db.BackupSettings.AsNoTracking()
            .FirstAsync(x => x.Id == BackupSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);
        return Math.Max(singleton.RetentionDays, BackupStrategyPolicy.SystemRetentionDays);
    }
}

/// <summary>Outcome of <see cref="AutomaticCleanupService.RunCleanupPassAsync"/>.</summary>
public readonly record struct AutomaticCleanupPassResult(int RunsDeleted, int TiersUpdated);
