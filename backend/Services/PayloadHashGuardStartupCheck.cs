using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Optional one-time startup check: if RunStartupCheck is true, samples offline_transactions and logs a warning when payload_hash mismatch ratio is high (ops mode).
/// </summary>
public sealed class PayloadHashGuardStartupCheck : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PayloadHashGuardStartupCheck> _logger;
    private const int DelaySeconds = 5;
    private const int SampleSize = 500;

    public PayloadHashGuardStartupCheck(
        IServiceProvider serviceProvider,
        ILogger<PayloadHashGuardStartupCheck> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(DelaySeconds), stoppingToken).ConfigureAwait(false);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var options = scope.ServiceProvider.GetService<IOptionsMonitor<PayloadHashGuardOptions>>()?.CurrentValue;
            if (options == null || !options.RunStartupCheck)
            {
                _logger.LogDebug("Payload hash startup check disabled (PayloadHashGuard:RunStartupCheck=false or not configured).");
                return;
            }

            var maintenance = scope.ServiceProvider.GetRequiredService<IOfflinePayloadHashMaintenanceService>();
            var result = await maintenance.AnalyzeAsync(SampleSize, null, stoppingToken).ConfigureAwait(false);

            if (result.LegacyDataQualityRiskHigh)
            {
                _logger.LogWarning(
                    "PayloadHashGuard startup check: legacy data quality risk HIGH. Mismatch ratio {MismatchRatioPercent:F1}% (threshold {Threshold}%), scanned={Scanned}. Run POST /api/admin/offline-payload-hash/analyze and repair before production.",
                    result.MismatchRatioPercent,
                    options.MismatchWarningThresholdPercent,
                    result.Scanned);
            }
            else
            {
                _logger.LogInformation(
                    "PayloadHashGuard startup check: mismatch ratio {MismatchRatioPercent:F1}% within threshold (scanned={Scanned}).",
                    result.MismatchRatioPercent,
                    result.Scanned);
            }
        }
        catch (OperationCanceledException)
        {
            // App shutting down
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Payload hash startup check failed; legacy risk not evaluated.");
        }
    }
}
