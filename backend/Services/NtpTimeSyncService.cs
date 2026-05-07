using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services;

/// <summary>
/// Background NTP sampling for RKSV clock drift detection; persists rows to <see cref="Models.SystemTimeSyncLog"/>.
/// Interval and enable flag follow effective settings (DB + appsettings).
/// </summary>
public sealed class NtpTimeSyncService : BackgroundService
{
    private readonly INtpEffectiveSettingsProvider _effectiveSettings;
    private readonly INtpSynchronizationCoordinator _coordinator;
    private readonly ILogger<NtpTimeSyncService> _logger;

    private static readonly TimeSpan DisabledPollInterval = TimeSpan.FromMinutes(1);

    public NtpTimeSyncService(
        INtpEffectiveSettingsProvider effectiveSettings,
        INtpSynchronizationCoordinator coordinator,
        ILogger<NtpTimeSyncService> logger)
    {
        _effectiveSettings = effectiveSettings;
        _coordinator = coordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            NtpSettings eff;
            try
            {
                eff = await _effectiveSettings.GetEffectiveAsync(stoppingToken).ConfigureAwait(false);
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

            await _coordinator.RunSynchronizationCycleAsync(eff, ignoreDisabled: false, stoppingToken).ConfigureAwait(false);

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
