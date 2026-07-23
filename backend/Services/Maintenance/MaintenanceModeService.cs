using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Maintenance;

public sealed class MaintenanceModeService : IMaintenanceModeService
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromHours(2);

    private readonly AppDbContext _db;
    private readonly IMaintenanceNotificationService _notifications;

    public MaintenanceModeService(AppDbContext db, IMaintenanceNotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<MaintenanceModeStatusDto> GetCurrentStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var active = await FindActiveWindowAsync(now, cancellationToken).ConfigureAwait(false);
        return Map(active, now);
    }

    public async Task<MaintenanceModeStatusDto> StartAsync(
        string actorUserId,
        StartMaintenanceModeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var existing = await FindActiveWindowAsync(now, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return Map(existing, now);

        var end = request.ScheduledEndAt.HasValue
            ? EnsureUtc(request.ScheduledEndAt.Value)
            : now.Add(DefaultDuration);
        if (end <= now)
            throw new ArgumentException("ScheduledEndAt must be in the future.");

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? "System maintenance"
            : request.Title.Trim();
        var message = string.IsNullOrWhiteSpace(request.Message)
            ? "The system is undergoing scheduled maintenance. Some operations are temporarily unavailable."
            : request.Message.Trim();

        var created = await _notifications
            .CreateAsync(
                actorUserId,
                new CreateMaintenanceNotificationRequestDto
                {
                    Title = title,
                    Message = message,
                    ScheduledStartAt = now,
                    ScheduledEndAt = end,
                    Priority = Math.Clamp(request.Priority, 1, 5),
                    IsMandatory = request.IsMandatory,
                    IsForceDisplay = true,
                    ForceDisplayFrom = now,
                    AffectedSystems = MaintenanceAffectedSystems.All,
                    PublishImmediately = true,
                },
                cancellationToken)
            .ConfigureAwait(false);

        await _notifications.MarkInProgressAsync(created.Id, cancellationToken).ConfigureAwait(false);
        var entity = await _db.MaintenanceNotifications
            .AsNoTracking()
            .FirstAsync(n => n.Id == created.Id, cancellationToken)
            .ConfigureAwait(false);
        return Map(entity, DateTime.UtcNow);
    }

    public async Task<MaintenanceModeStatusDto> EndAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var windows = await _db.MaintenanceNotifications
            .Where(n =>
                n.Status == MaintenanceNotificationStatuses.InProgress
                || (n.Status == MaintenanceNotificationStatuses.Published
                    && n.ScheduledStartAt <= now
                    && n.ScheduledEndAt > now))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in windows)
        {
            row.Status = MaintenanceNotificationStatuses.Completed;
            row.UpdatedAt = now;
        }

        if (windows.Count > 0)
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<MaintenanceNotification?> FindActiveWindowAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        return await _db.MaintenanceNotifications.AsNoTracking()
            .Where(n =>
                n.ScheduledEndAt > utcNow
                && (n.Status == MaintenanceNotificationStatuses.InProgress
                    || (n.Status == MaintenanceNotificationStatuses.Published
                        && n.ScheduledStartAt <= utcNow)))
            .OrderByDescending(n => n.Priority)
            .ThenBy(n => n.ScheduledStartAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static MaintenanceModeStatusDto Map(MaintenanceNotification? entity, DateTime utcNow)
    {
        if (entity is null)
        {
            return new MaintenanceModeStatusDto
            {
                IsActive = false,
                Status = "Inactive",
                BlocksPosPayments = false,
                BlocksApiWrites = false,
            };
        }

        var isActive = entity.ScheduledEndAt > utcNow
            && (entity.Status == MaintenanceNotificationStatuses.InProgress
                || (entity.Status == MaintenanceNotificationStatuses.Published
                    && entity.ScheduledStartAt <= utcNow));

        return new MaintenanceModeStatusDto
        {
            IsActive = isActive,
            NotificationId = entity.Id,
            Title = entity.Title,
            Message = entity.Message,
            StartedAt = entity.PublishedAt ?? entity.CreatedAt,
            ScheduledStartAt = entity.ScheduledStartAt,
            ScheduledEndAt = entity.ScheduledEndAt,
            Status = isActive ? entity.Status : "Inactive",
            BlocksPosPayments = isActive,
            BlocksApiWrites = isActive,
        };
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
