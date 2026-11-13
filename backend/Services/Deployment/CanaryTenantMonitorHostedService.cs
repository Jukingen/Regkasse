using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Deployment;

/// <summary>
/// Periodically evaluates canary-soaking tenants for elevated audit failures and publishes activity alerts.
/// </summary>
public sealed class CanaryTenantMonitorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<DeploymentOptions> _options;
    private readonly ILogger<CanaryTenantMonitorHostedService> _logger;

    public CanaryTenantMonitorHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<DeploymentOptions> options,
        ILogger<CanaryTenantMonitorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var monitor = _options.CurrentValue.CanaryMonitor;
            var interval = TimeSpan.FromMinutes(Math.Max(5, monitor.CheckIntervalMinutes));

            try
            {
                if (monitor.Enabled)
                    await EvaluateAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Canary tenant monitor cycle failed");
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

    private async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var monitor = opts.CanaryMonitor;
        var window = TimeSpan.FromMinutes(Math.Max(5, monitor.WindowMinutes));
        var since = DateTime.UtcNow - window;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var activity = scope.ServiceProvider.GetRequiredService<IActivityEventPublisher>();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var soaking = await db.TenantDeploymentHistories.AsNoTracking()
            .Where(h => h.Status == "canary_soak"
                        && h.Stage == "canary"
                        && (h.SoakUntilUtc == null || h.SoakUntilUtc > DateTime.UtcNow))
            .Select(h => new { h.TenantId, h.Version })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Also include configured canary tenants even if soak status not set
        var configuredIds = opts.CanaryTenantIds.ToList();
        if (opts.CanaryTenantSlugs.Length > 0)
        {
            var slugIds = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
                .Where(t => opts.CanaryTenantSlugs.Contains(t.Slug))
                .Select(t => t.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            configuredIds.AddRange(slugIds);
        }

        var tenantIds = soaking.Select(s => s.TenantId)
            .Concat(configuredIds)
            .Distinct()
            .ToList();

        foreach (var tenantId in tenantIds)
        {
            var total = await db.AuditLogs.IgnoreQueryFilters().AsNoTracking()
                .CountAsync(
                    a => a.TenantId == tenantId && a.Timestamp >= since,
                    cancellationToken)
                .ConfigureAwait(false);

            var failed = await db.AuditLogs.IgnoreQueryFilters().AsNoTracking()
                .CountAsync(
                    a => a.TenantId == tenantId
                         && a.Timestamp >= since
                         && a.Status == AuditLogStatus.Failed,
                    cancellationToken)
                .ConfigureAwait(false);

            var version = soaking.FirstOrDefault(s => s.TenantId == tenantId)?.Version;

            if (failed >= monitor.ErrorCountThreshold)
            {
                await activity.TryPublishAsync(
                    tenantId,
                    ActivityEventType.CanaryTenantErrors,
                    metadata: new
                    {
                        FailedCount = failed,
                        TotalCount = total,
                        WindowMinutes = monitor.WindowMinutes,
                        Version = version,
                        Threshold = monitor.ErrorCountThreshold,
                    },
                    dedupKey: $"canary-errors:{tenantId:D}:{DateTime.UtcNow:yyyyMMddHH}",
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                _logger.LogWarning(
                    "Canary tenant {TenantId} error count {Failed}/{Total} in {Window}m (version={Version})",
                    tenantId, failed, total, monitor.WindowMinutes, version);
            }

            if (total >= monitor.MinEventsForRate && total > 0)
            {
                var rate = 100.0 * failed / total;
                if (rate >= monitor.ErrorRateThresholdPercent)
                {
                    await activity.TryPublishAsync(
                        tenantId,
                        ActivityEventType.CanaryTenantHighErrorRate,
                        metadata: new
                        {
                            FailedCount = failed,
                            TotalCount = total,
                            ErrorRatePercent = Math.Round(rate, 2),
                            WindowMinutes = monitor.WindowMinutes,
                            Version = version,
                            ThresholdPercent = monitor.ErrorRateThresholdPercent,
                        },
                        dedupKey: $"canary-rate:{tenantId:D}:{DateTime.UtcNow:yyyyMMddHH}",
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    _logger.LogWarning(
                        "Canary tenant {TenantId} high error rate {Rate}% ({Failed}/{Total}) version={Version}",
                        tenantId, rate, failed, total, version);
                }
            }
        }
    }
}
