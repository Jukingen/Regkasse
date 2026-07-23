using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Offline;

/// <summary>
/// Background offline alert runner: evaluates <see cref="IOfflineMonitoringService"/> anomalies
/// per tenant and dispatches critical alerts to the activity notification pipeline.
/// </summary>
public sealed class OfflineAlertService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OfflineAlertRules> _rules;
    private readonly ILogger<OfflineAlertService> _logger;

    public OfflineAlertService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OfflineAlertRules> rules,
        ILogger<OfflineAlertService> logger)
    {
        _scopeFactory = scopeFactory;
        _rules = rules;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAlertsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in offline alert service");
            }

            var intervalSeconds = Math.Clamp(_rules.CurrentValue.CheckIntervalSeconds, 30, 86_400);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task CheckAlertsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenants = await db.Tenants
            .AsNoTracking()
            .Where(t => t.DeletedAtUtc == null && t.Status == TenantStatuses.Active)
            .Select(t => t.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var tenantId in tenants)
        {
            try
            {
                await CheckTenantAlertsAsync(scope.ServiceProvider, tenantId, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Offline alert check failed for tenant {TenantId}", tenantId);
            }
        }
    }

    private async Task CheckTenantAlertsAsync(
        IServiceProvider scopedProvider,
        Guid tenantId,
        CancellationToken ct)
    {
        var tenantAccessor = scopedProvider.GetRequiredService<ICurrentTenantAccessor>();
        tenantAccessor.TenantId = tenantId;

        var monitoring = scopedProvider.GetRequiredService<IOfflineMonitoringService>();
        var anomalies = await monitoring.CheckAnomaliesAsync(ct).ConfigureAwait(false);

        foreach (var anomaly in anomalies)
        {
            if (string.Equals(anomaly.Severity, "critical", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Critical offline anomaly for tenant {TenantId}: [{Code}] {Message}",
                    tenantId,
                    anomaly.Code,
                    anomaly.Message);
                await SendAlertAsync(tenantId, anomaly, scopedProvider, ct).ConfigureAwait(false);
            }
            else if (string.Equals(anomaly.Severity, "warning", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Offline warning for tenant {TenantId}: [{Code}] {Message}",
                    tenantId,
                    anomaly.Code,
                    anomaly.Message);
            }
        }

        // Dedicated TSE NonFiscalPending queue depth alert (legacy offline_transactions).
        var tseQueue = scopedProvider.GetRequiredService<ITseOfflineQueueService>();
        var queueStatus = await tseQueue.GetQueueStatusAsync(tenantId, ct).ConfigureAwait(false);
        if (queueStatus.IsWarning || queueStatus.IsCritical)
        {
            await tseQueue
                .SendQueueAlertAsync(tenantId, queueStatus.TotalQueued, ct)
                .ConfigureAwait(false);
        }
    }

    private static async Task SendAlertAsync(
        Guid tenantId,
        OfflineAnomaly anomaly,
        IServiceProvider scopedProvider,
        CancellationToken ct)
    {
        var (eventType, dedupKey) = MapAnomaly(anomaly);
        if (eventType == null)
            return;

        var activity = scopedProvider.GetRequiredService<IActivityEventService>();
        await activity.PublishAsync(
            new ActivityEventPublishRequest(
                tenantId,
                eventType.Value,
                Title: anomaly.Message,
                Description: $"[{anomaly.Code}] {anomaly.Message}",
                DedupKey: dedupKey,
                EntityType: "offline_monitoring",
                EntityId: anomaly.Code,
                Metadata: new Dictionary<string, object>
                {
                    ["code"] = anomaly.Code,
                    ["severity"] = anomaly.Severity,
                    ["detectedAtUtc"] = anomaly.DetectedAt,
                }),
            ct).ConfigureAwait(false);
    }

    internal static (ActivityEventType? Type, string DedupKey) MapAnomaly(OfflineAnomaly anomaly) =>
        anomaly.Code switch
        {
            "too_many_pending" when anomaly.Message.Contains("offline orders", StringComparison.OrdinalIgnoreCase) =>
                (ActivityEventType.OfflineOrdersBacklogGrowing, "offline_orders_backlog_tenant"),
            "too_many_pending" =>
                (ActivityEventType.OfflineQueueGrowing, "offline_tx_backlog_tenant"),
            "old_pending" =>
                (ActivityEventType.OfflineOrdersExpiringSoon, "offline_orders_expiry_tenant"),
            "sync_failure" =>
                (ActivityEventType.OfflineSyncStalled, $"offline_sync_failure_{anomaly.Severity}"),
            "expired_pending" =>
                (ActivityEventType.OfflineSyncStalled, "offline_orders_expired_pending"),
            "tse_cap_warning" or "tse_cap_reached" =>
                (ActivityEventType.OfflineQueueGrowing, "offline_tx_tse_cap"),
            "clock_drift" =>
                (ActivityEventType.OfflineQueueGrowing, "offline_tx_clock_drift"),
            "sequence_gap" =>
                (ActivityEventType.OfflineSyncStalled, "offline_tx_sequence_gap"),
            _ => (null, ""),
        };
}
