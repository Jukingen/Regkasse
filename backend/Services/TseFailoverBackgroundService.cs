using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Periodically evaluates primary TSE devices for automatic failover / revert.
/// Complements the process-wide probe <see cref="TseHealthCheckService"/>; uses
/// <see cref="ITseFailoverService.CheckAndFailoverAsync"/> (fresh health + failover or revert).
/// </summary>
public sealed class TseFailoverBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ILogger<TseFailoverBackgroundService> _logger;

    public TseFailoverBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TseOptions> tseOptions,
        ILogger<TseFailoverBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _tseOptions = tseOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger slightly so we do not always align with TseHealthCheckService ticks.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _tseOptions.CurrentValue;
            var interval = TimeSpan.FromSeconds(Math.Clamp(opts.HealthCheckIntervalSeconds, 5, 600));

            try
            {
                if (opts.AutoFailoverEnabled && !opts.IsOff)
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    await RunCycleAsync(scope.ServiceProvider, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE failover background service error");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// One evaluation pass over active primary devices (also used by tests).
    /// <see cref="ITseFailoverService.CheckAndFailoverAsync"/> both fails over unhealthy
    /// primaries and reverts when the primary is healthy again under an active backup.
    /// </summary>
    internal static async Task RunCycleAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var failoverService = services.GetRequiredService<ITseFailoverService>();
        var db = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILogger<TseFailoverBackgroundService>>();

        // Primaries only — backup rows use IsPrimary=false. IsFailoverActive lives on backups.
        var primaryIds = await db.TseDevices
            .AsNoTracking()
            .Where(d => d.IsPrimary && d.IsActive)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var deviceId in primaryIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await failoverService
                    .CheckAndFailoverAsync(deviceId, cancellationToken)
                    .ConfigureAwait(false);

                if (result.Succeeded
                    && result.Message.Contains("Failover to backup", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(
                        "TSE automatic failover activated for primary {DeviceId}: {Message}",
                        deviceId,
                        result.Message);
                }
                else if (result.Succeeded
                         && result.Message.Contains("Reverted", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation(
                        "TSE failover reverted to primary {DeviceId}: {Message}",
                        deviceId,
                        result.Message);
                }
                else if (!result.IsSuccess && result.NeedsAttention)
                {
                    logger.LogWarning(
                        "TSE device {DeviceId} needs attention: {Message}",
                        deviceId,
                        result.Message);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "TSE failover check failed for primary device {DeviceId}",
                    deviceId);
            }
        }

        try
        {
            var certService = services.GetRequiredService<ITseCertificateService>();
            await certService.ProcessExpiryWarningsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TSE certificate expiry warning pass failed");
        }

        try
        {
            var performance = services.GetRequiredService<ITsePerformanceService>();
            await performance.ProcessPerformanceAnomaliesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TSE performance anomaly pass failed");
        }
    }
}
