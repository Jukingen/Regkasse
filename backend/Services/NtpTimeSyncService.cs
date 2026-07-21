using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services;

/// <summary>
/// Background NTP sampling for RKSV clock drift detection; persists rows to <see cref="Models.SystemTimeSyncLog"/>.
/// Interval and enable flag follow effective settings (DB + appsettings).
/// </summary>
/// <remarks>
/// Online fiscal payment blocking when NTP is enabled is implemented on <see cref="INtpTimeSyncStatus.ShouldAllowOnlineFiscalPayment"/>
/// (<see cref="NtpTimeSyncStatus"/>) — not in this hosted loop.
/// </remarks>
public sealed class NtpTimeSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NtpTimeSyncService> _logger;

    private static readonly TimeSpan DisabledPollInterval = TimeSpan.FromMinutes(1);

    public NtpTimeSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<NtpTimeSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            NtpSettings eff;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var effectiveSettings = scope.ServiceProvider.GetRequiredService<INtpEffectiveSettingsProvider>();
                eff = await effectiveSettings.GetEffectiveAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load effective NTP settings; retrying.");
                try
                {
                    await Task.Delay(DisabledPollInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            if (!eff.Enabled)
            {
                _logger.LogInformation("NTP auto-sync is disabled (effective AutoSyncEnabled=false).");
                try
                {
                    await Task.Delay(DisabledPollInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var coordinator = scope.ServiceProvider.GetRequiredService<INtpSynchronizationCoordinator>();
                await coordinator.RunSynchronizationCycleAsync(eff, ignoreDisabled: false, stoppingToken).ConfigureAwait(false);
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, eff.SyncIntervalMinutes));
            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
