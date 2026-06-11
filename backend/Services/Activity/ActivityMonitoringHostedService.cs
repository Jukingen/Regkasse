using KasseAPI_Final;
using KasseAPI_Final.Configuration;
using static KasseAPI_Final.Configuration.LicenseGracePeriodConfig;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AdminTenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Activity;

/// <summary>
/// Periodic tenant sweep: offline queue depth and license expiry anchors for activity feed.
/// </summary>
public sealed class ActivityMonitoringHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<ActivityNotificationOptions> _options;
    private readonly ILogger<ActivityMonitoringHostedService> _logger;

    public ActivityMonitoringHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<ActivityNotificationOptions> options,
        ILogger<ActivityMonitoringHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = Math.Clamp(_options.CurrentValue.MonitorIntervalMinutes, 1, 120);
            try
            {
                await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Activity monitoring cycle failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(interval), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activity = scope.ServiceProvider.GetRequiredService<IActivityEventService>();

        var threshold = Math.Max(1, _options.CurrentValue.OfflineQueueAlertThreshold);

        var tenants = await db.Tenants
            .AsNoTracking()
            .Where(t => t.DeletedAtUtc == null && t.Status == TenantStatuses.Active)
            .Select(t => new { t.Id, t.LicenseValidUntilUtc, t.Slug })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var tenant in tenants)
        {
            await CheckOfflineQueuesAsync(db, activity, tenant.Id, threshold, cancellationToken).ConfigureAwait(false);
            await CheckLicenseExpiryAsync(activity, tenant.Id, tenant.LicenseValidUntilUtc, tenant.Slug, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task CheckOfflineQueuesAsync(
        AppDbContext db,
        IActivityEventService activity,
        Guid tenantId,
        int threshold,
        CancellationToken cancellationToken)
    {
        var counts = await db.OfflineTransactions
            .IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId && o.Status == OfflineTransactionStatus.Pending)
            .GroupBy(o => o.CashRegisterId)
            .Select(g => new { RegisterId = g.Key, Count = g.Count() })
            .Where(x => x.Count > threshold)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in counts)
        {
            await activity.PublishAsync(
                new ActivityEventPublishRequest(
                    tenantId,
                    ActivityEventType.OfflineQueueGrowing,
                    "Offline queue growing",
                    Description: $"Cash register {row.RegisterId} has {row.Count} pending offline intents (threshold {threshold}).",
                    DedupKey: $"offline_queue_{row.RegisterId}",
                    EntityType: "cash_register",
                    EntityId: row.RegisterId.ToString()),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task CheckLicenseExpiryAsync(
        IActivityEventService activity,
        Guid tenantId,
        DateTime? licenseValidUntilUtc,
        string? slug,
        CancellationToken cancellationToken)
    {
        if (licenseValidUntilUtc == null)
            return;

        var days = (int)Math.Ceiling((licenseValidUntilUtc.Value - DateTime.UtcNow).TotalDays);
        if (days <= 0)
        {
            await activity.PublishAsync(
                new ActivityEventPublishRequest(
                    tenantId,
                    ActivityEventType.LicenseExpired,
                    "License expired",
                    Description: $"Tenant {(slug ?? tenantId.ToString())} license expired.",
                    DedupKey: "license_expired"),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        int[] anchors = [30, WarningDaysBeforeExpiry, 7];
        foreach (var anchor in anchors)
        {
            if (days > anchor)
                continue;

            await activity.PublishAsync(
                new ActivityEventPublishRequest(
                    tenantId,
                    ActivityEventType.LicenseExpiringSoon,
                    $"License expires in {days} day(s)",
                    Description: $"Tenant {(slug ?? tenantId.ToString())} license expires on {licenseValidUntilUtc:yyyy-MM-dd} UTC.",
                    DedupKey: $"license_expiry_{anchor}d"),
                cancellationToken).ConfigureAwait(false);
            break;
        }
    }
}
