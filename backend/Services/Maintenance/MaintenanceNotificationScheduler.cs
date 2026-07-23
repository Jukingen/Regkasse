using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Maintenance;

/// <summary>
/// Polls published maintenance windows every 5 minutes:
/// milestone activity feed alerts (7d / 3d / 24h / 1h), force-display enable at 24h,
/// and auto-transition to InProgress when the start time is reached.
/// </summary>
public sealed class MaintenanceNotificationScheduler : BackgroundService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceNotificationScheduler> _logger;

    public MaintenanceNotificationScheduler(
        IServiceScopeFactory scopeFactory,
        ILogger<MaintenanceNotificationScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        // Stagger first run slightly so startup storms don't all hit DB together.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledNotificationsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Maintenance notification scheduler cycle failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>Visible for unit tests — processes one scheduler cycle.</summary>
    internal async Task ProcessScheduledNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activity = scope.ServiceProvider.GetRequiredService<IActivityEventPublisher>();

        var now = DateTime.UtcNow;
        var upcoming = await db.MaintenanceNotifications
            .Where(n =>
                n.Status == MaintenanceNotificationStatuses.Published
                && n.ScheduledEndAt > now)
            .OrderBy(n => n.ScheduledStartAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (upcoming.Count == 0)
            return;

        var tenantIds = await db.Tenants.AsNoTracking()
            .Where(t => t.DeletedAtUtc == null && t.Status == TenantStatuses.Active)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dirty = false;
        foreach (var maintenance in upcoming)
        {
            if (await TryAdvanceLifecycleAsync(activity, tenantIds, maintenance, now, cancellationToken)
                    .ConfigureAwait(false))
                dirty = true;

            if (await TryProcessMilestonesAsync(activity, tenantIds, maintenance, now, cancellationToken)
                    .ConfigureAwait(false))
                dirty = true;
        }

        if (dirty)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> TryAdvanceLifecycleAsync(
        IActivityEventPublisher activity,
        IReadOnlyList<Guid> tenantIds,
        MaintenanceNotification maintenance,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Auto-start when the scheduled window begins.
        if (maintenance.Status == MaintenanceNotificationStatuses.Published
            && maintenance.ScheduledStartAt <= now
            && maintenance.ScheduledEndAt > now)
        {
            maintenance.Status = MaintenanceNotificationStatuses.InProgress;
            maintenance.UpdatedAt = now;
            maintenance.IsForceDisplay = true;
            maintenance.ForceDisplayFrom ??= now;
            await BroadcastAsync(
                    activity,
                    tenantIds,
                    ActivityEventType.MaintenanceStarted,
                    maintenance,
                    milestoneKey: "started",
                    messageSuffix: "Maintenance window has started.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async Task<bool> TryProcessMilestonesAsync(
        IActivityEventPublisher activity,
        IReadOnlyList<Guid> tenantIds,
        MaintenanceNotification maintenance,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Only pre-start reminders for Published (not yet started) windows.
        if (maintenance.Status != MaintenanceNotificationStatuses.Published)
            return false;
        if (maintenance.ScheduledStartAt <= now)
            return false;

        var timeUntilStart = maintenance.ScheduledStartAt - now;
        var changed = false;

        // 7 days (±1 day window for 5-min poll)
        if (maintenance.Reminder7dSentAt is null
            && timeUntilStart <= TimeSpan.FromDays(7)
            && timeUntilStart > TimeSpan.FromDays(6))
        {
            await BroadcastAsync(
                    activity,
                    tenantIds,
                    ActivityEventType.MaintenanceUpcoming,
                    maintenance,
                    milestoneKey: "7d",
                    messageSuffix: "1 week until maintenance.",
                    cancellationToken)
                .ConfigureAwait(false);
            maintenance.Reminder7dSentAt = now;
            changed = true;
        }

        // 3 days
        if (maintenance.Reminder3dSentAt is null
            && timeUntilStart <= TimeSpan.FromDays(3)
            && timeUntilStart > TimeSpan.FromDays(2))
        {
            await BroadcastAsync(
                    activity,
                    tenantIds,
                    ActivityEventType.MaintenanceUpcoming,
                    maintenance,
                    milestoneKey: "3d",
                    messageSuffix: "3 days until maintenance.",
                    cancellationToken)
                .ConfigureAwait(false);
            maintenance.Reminder3dSentAt = now;
            changed = true;
        }

        // 24 hours — enable force display
        if (maintenance.Reminder24hSentAt is null
            && timeUntilStart <= TimeSpan.FromHours(24)
            && timeUntilStart > TimeSpan.FromHours(12))
        {
            maintenance.IsForceDisplay = true;
            maintenance.ForceDisplayFrom = now;
            await BroadcastAsync(
                    activity,
                    tenantIds,
                    ActivityEventType.MaintenanceForceDisplayEnabled,
                    maintenance,
                    milestoneKey: "24h",
                    messageSuffix: "24 hours until maintenance. Clients will force-display this notice.",
                    cancellationToken)
                .ConfigureAwait(false);
            maintenance.Reminder24hSentAt = now;
            changed = true;
        }

        // 1 hour
        if (maintenance.Reminder1hSentAt is null
            && timeUntilStart <= TimeSpan.FromHours(1)
            && timeUntilStart > TimeSpan.FromMinutes(30))
        {
            await BroadcastAsync(
                    activity,
                    tenantIds,
                    ActivityEventType.MaintenanceUpcoming,
                    maintenance,
                    milestoneKey: "1h",
                    messageSuffix: "1 hour until maintenance.",
                    cancellationToken)
                .ConfigureAwait(false);
            maintenance.Reminder1hSentAt = now;
            changed = true;
        }

        // Catch-up: if somehow past 24h window without milestone (e.g. published late), still force-display.
        if (timeUntilStart <= TimeSpan.FromHours(24)
            && !maintenance.IsForceDisplay
            && maintenance.Reminder24hSentAt is null)
        {
            maintenance.IsForceDisplay = true;
            maintenance.ForceDisplayFrom ??= now;
            maintenance.Reminder24hSentAt = now;
            await BroadcastAsync(
                    activity,
                    tenantIds,
                    ActivityEventType.MaintenanceForceDisplayEnabled,
                    maintenance,
                    milestoneKey: "24h-catchup",
                    messageSuffix: "Less than 24 hours until maintenance. Force display enabled.",
                    cancellationToken)
                .ConfigureAwait(false);
            changed = true;
        }

        if (changed)
            maintenance.UpdatedAt = now;

        return changed;
    }

    private static async Task BroadcastAsync(
        IActivityEventPublisher activity,
        IReadOnlyList<Guid> tenantIds,
        ActivityEventType type,
        MaintenanceNotification maintenance,
        string milestoneKey,
        string messageSuffix,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, object>
        {
            ["MaintenanceId"] = maintenance.Id.ToString("D"),
            ["Title"] = maintenance.Title,
            ["Message"] = $"{messageSuffix} {maintenance.Message}".Trim(),
            ["ScheduledStartAt"] = maintenance.ScheduledStartAt.ToString("O"),
            ["ScheduledEndAt"] = maintenance.ScheduledEndAt.ToString("O"),
            ["IsForceDisplay"] = maintenance.IsForceDisplay,
            ["Milestone"] = milestoneKey,
        };

        foreach (var tenantId in tenantIds)
        {
            await activity
                .TryPublishAsync(
                    tenantId,
                    type,
                    metadata,
                    actorUserId: null,
                    dedupKey: $"maintenance_{maintenance.Id:N}_{milestoneKey}",
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
