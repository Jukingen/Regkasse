using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Periodic job: repair legacy payload_hash in batches. Conflict strategy: report only (skip conflicting rows, log and metric).
/// Updates completion metric (%) after each cycle so fallback can be retired when 100%.
/// </summary>
public sealed class PayloadHashRepairHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<PayloadHashRepairJobOptions> _options;
    private readonly ICoreMetrics? _metrics;
    private readonly ILogger<PayloadHashRepairHostedService> _logger;

    public PayloadHashRepairHostedService(
        IServiceProvider serviceProvider,
        IOptionsMonitor<PayloadHashRepairJobOptions> options,
        ILogger<PayloadHashRepairHostedService> logger,
        ICoreMetrics? metrics = null)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            _logger.LogInformation("Payload hash repair job is disabled (PayloadHashRepairJob:Enabled=false).");
            return;
        }

        _logger.LogInformation(
            "Payload hash repair job started. Interval={Interval}, BatchSize={BatchSize}, MaxBatchesPerCycle={MaxBatches}",
            opts.Interval, opts.BatchSizePerCycle, opts.MaxBatchesPerCycle);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOneCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payload hash repair job cycle failed.");
            }

            try
            {
                await Task.Delay(opts.Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOneCycleAsync(CancellationToken stoppingToken)
    {
        var opts = _options.CurrentValue;
        using var scope = _serviceProvider.CreateScope();
        var maintenance = scope.ServiceProvider.GetRequiredService<IOfflinePayloadHashMaintenanceService>();

        var totalUpdated = 0;
        var totalConflict = 0;
        var batches = 0;

        while (batches < opts.MaxBatchesPerCycle)
        {
            stoppingToken.ThrowIfCancellationRequested();
            var result = await maintenance.RepairAsync(opts.BatchSizePerCycle, dryRun: false, cashRegisterId: null, stoppingToken).ConfigureAwait(false);

            totalUpdated += result.Updated;
            totalConflict += result.SkippedConflict;
            batches++;

            if (result.SkippedConflict > 0)
            {
                _metrics?.RecordPayloadHashRepairConflict(result.SkippedConflict);
                _logger.LogWarning(
                    "Payload hash repair: conflict (report only). Skipped {Count} row(s); (CashRegisterId, canonicalHash) already occupied. No auto-resolution.",
                    result.SkippedConflict);
            }

            if (result.Updated == 0)
                break;
        }

        if (totalUpdated > 0)
            _logger.LogInformation("Payload hash repair cycle: updated {Updated} row(s), batches={Batches}.", totalUpdated, batches);
        if (totalConflict > 0)
            _logger.LogInformation("Payload hash repair cycle: total conflicts reported (skipped)={Conflicts}.", totalConflict);

        var analyze = await maintenance.AnalyzeAsync(opts.CompletionSampleSize, null, stoppingToken).ConfigureAwait(false);
        var completionPercent = analyze.Scanned > 0 ? 100.0 - analyze.MismatchRatioPercent : 100.0;
        _metrics?.SetPayloadHashCompletionPercent(completionPercent);
        _logger.LogDebug(
            "Payload hash completion: {Completion:F1}% (mismatch {Mismatch:F1}%, sampled n={Scanned}).",
            completionPercent, analyze.MismatchRatioPercent, analyze.Scanned);
    }
}
