using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.OperationalRuns;

/// <summary>
/// Periyodik stale lease taraması; backup ve restore drill için ortak döngü (en küçük scan interval kullanılır).
/// </summary>
public sealed class StaleRunReaperHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<BackupOptions> _backupOptions;
    private readonly IOptionsMonitor<RestoreVerificationOptions> _restoreOptions;
    private readonly IDrStaleRunRecoveryObserver _staleObserver;
    private readonly ILogger<StaleRunReaperHostedService> _logger;

    public StaleRunReaperHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<BackupOptions> backupOptions,
        IOptionsMonitor<RestoreVerificationOptions> restoreOptions,
        IDrStaleRunRecoveryObserver staleObserver,
        ILogger<StaleRunReaperHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _backupOptions = backupOptions;
        _restoreOptions = restoreOptions;
        _staleObserver = staleObserver;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromMinutes(1);
            try
            {
                var b = _backupOptions.CurrentValue.StaleRecoveryScanInterval;
                var r = _restoreOptions.CurrentValue.StaleRecoveryScanInterval;
                delay = b <= r ? b : r;
                if (delay < TimeSpan.FromSeconds(5))
                    delay = TimeSpan.FromSeconds(5);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var backupOpts = _backupOptions.CurrentValue;
                var restoreOpts = _restoreOptions.CurrentValue;
                var leaseOpts = new StaleRunReaperLeaseOptions(
                    backupOpts.RunLeaseTimeout,
                    backupOpts.StaleRecoveryNullLeaseGraceMultiplier,
                    restoreOpts.RunLeaseTimeout,
                    restoreOpts.StaleRecoveryNullLeaseGraceMultiplier);
                var recoveredBackupIds = await StaleRunReaper
                    .RecoverStaleRunsAsync(db, DateTime.UtcNow, leaseOpts, _logger, _staleObserver, stoppingToken)
                    .ConfigureAwait(false);

                if (recoveredBackupIds.Count > 0 && backupOpts.AutomaticRetryMaxAttempts > 0)
                {
                    var utcNow = DateTime.UtcNow;
                    foreach (var runId in recoveredBackupIds)
                    {
                        var run = await db.BackupRuns.FirstOrDefaultAsync(r => r.Id == runId, stoppingToken)
                            .ConfigureAwait(false);
                        if (run == null)
                            continue;
                        await BackupAutomaticRetryCoordinator
                            .TrySchedulePendingRetryAfterTerminalSaveAsync(
                                db,
                                run,
                                backupOpts,
                                utcNow,
                                _logger,
                                stoppingToken)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stale run reaper tick failed");
            }

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
}
