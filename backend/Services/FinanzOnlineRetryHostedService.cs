using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Background job: periodically retries FinanzOnline submit for Pending payments with exponential backoff and max retry limit.
/// Does not resubmit already-Submitted payments (duplicate submit safe). Emits alerts when failed count or per-register repeated failures exceed thresholds.
/// </summary>
public sealed class FinanzOnlineRetryHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<FinanzOnlineRetryJobOptions> _options;
    private readonly IFinanzOnlineMetrics _metrics;
    private readonly IFinanzOnlineAlertSink _alertSink;
    private readonly ILogger<FinanzOnlineRetryHostedService> _logger;

    public FinanzOnlineRetryHostedService(
        IServiceProvider serviceProvider,
        IOptionsMonitor<FinanzOnlineRetryJobOptions> options,
        IFinanzOnlineMetrics metrics,
        IFinanzOnlineAlertSink alertSink,
        ILogger<FinanzOnlineRetryHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _metrics = metrics;
        _alertSink = alertSink;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            _logger.LogInformation("FinanzOnline retry job is disabled (FinanzOnlineRetryJob:Enabled=false).");
            return;
        }

        _logger.LogInformation("FinanzOnline retry job started. Interval={Interval}, MaxRetryCount={MaxRetry}, BackoffBase={Base}s",
            opts.Interval, opts.MaxRetryCount, opts.BaseDelaySeconds);

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
                _logger.LogError(ex, "FinanzOnline retry job cycle failed.");
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

    private async Task RunOneCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var opts = _options.CurrentValue;

        var now = DateTime.UtcNow;
        int BackoffSeconds(int retryCount)
        {
            var delay = opts.BaseDelaySeconds * (int)Math.Pow(2, Math.Min(retryCount, 20));
            return Math.Min(delay, opts.BackoffCapSeconds);
        }

        // Pending only; RetryCount < MaxRetryCount; then filter by backoff in memory (per-row backoff depends on RetryCount)
        var rawCandidates = await context.PaymentDetails
            .AsNoTracking()
            .Where(p => p.FinanzOnlineStatus == "Pending" && p.FinanzOnlineRetryCount < opts.MaxRetryCount)
            .OrderBy(p => p.FinanzOnlineLastAttemptAtUtc ?? p.CreatedAt)
            .Take(opts.BatchSize * 2)
            .Select(p => new { p.Id, p.FinanzOnlineRetryCount, p.FinanzOnlineLastAttemptAtUtc, p.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var candidates = rawCandidates
            .Where(p => p.FinanzOnlineLastAttemptAtUtc == null ||
                p.FinanzOnlineLastAttemptAtUtc.Value.AddSeconds(BackoffSeconds(p.FinanzOnlineRetryCount)) <= now)
            .Take(opts.BatchSize)
            .Select(p => new { p.Id, p.FinanzOnlineRetryCount })
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        _logger.LogDebug("FinanzOnline retry cycle: {Count} pending candidate(s).", candidates.Count);

        var markMaxRetriesExceeded = new List<Guid>();

        foreach (var c in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _metrics.IncrementSubmitTotal();
            var result = await paymentService.RetryFinanzOnlineSubmitAsync(c.Id).ConfigureAwait(false);
            if (!result.Success)
            {
                _metrics.IncrementSubmitFailed(result.FailureKind);
                var newCount = c.FinanzOnlineRetryCount + 1;
                if (newCount >= opts.MaxRetryCount)
                    markMaxRetriesExceeded.Add(c.Id);
            }
        }

        if (markMaxRetriesExceeded.Count > 0)
        {
            foreach (var paymentId in markMaxRetriesExceeded)
            {
                var payment = await context.PaymentDetails.FindAsync(new object[] { paymentId }, cancellationToken).ConfigureAwait(false);
                if (payment != null && payment.FinanzOnlineStatus == "Pending")
                {
                    payment.FinanzOnlineStatus = "Failed";
                    const string suffix = " (Max retries exceeded).";
                    const int maxDbLen = 500;
                    if (string.IsNullOrEmpty(payment.FinanzOnlineError))
                        payment.FinanzOnlineError = suffix.TrimStart();
                    else if (payment.FinanzOnlineError.Length + suffix.Length <= maxDbLen)
                        payment.FinanzOnlineError += suffix;
                    else
                        payment.FinanzOnlineError = payment.FinanzOnlineError.Substring(0, maxDbLen - suffix.Length - 3) + "..." + suffix;
                }
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await EmitAlertsAsync(context, opts, cancellationToken).ConfigureAwait(false);
    }

    private async Task EmitAlertsAsync(AppDbContext context, FinanzOnlineRetryJobOptions opts, CancellationToken cancellationToken)
    {
        var failedCount = await context.PaymentDetails
            .CountAsync(p => p.FinanzOnlineStatus == "Failed", cancellationToken)
            .ConfigureAwait(false);

        if (failedCount > opts.AlertFailedThreshold)
        {
            _logger.LogWarning(
                "FinanzOnlineAlert: Failed count {FailedCount} exceeds threshold {Threshold}. Review GET /api/admin/finanzonline-reconciliation?status=Failed.",
                failedCount, opts.AlertFailedThreshold);
            _alertSink.OnFailedCountThresholdExceeded(failedCount, opts.AlertFailedThreshold);
        }

        var registerFailureCounts = await context.PaymentDetails
            .Where(p => p.FinanzOnlineStatus == "Failed" || (p.FinanzOnlineStatus == "Pending" && p.FinanzOnlineRetryCount >= opts.MaxRetryCount))
            .GroupBy(p => p.CashRegisterId)
            .Select(g => new { RegisterId = g.Key, Count = g.Count() })
            .Where(x => x.Count >= opts.RegisterRepeatedFailureThreshold)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var r in registerFailureCounts)
        {
            _logger.LogWarning(
                "FinanzOnlineAlert: Register {CashRegisterId} has {FailureCount} failed or max-retry FinanzOnline submissions.",
                r.RegisterId, r.Count);
            _alertSink.OnRegisterRepeatedFailure(r.RegisterId, r.Count);
        }
    }
}
