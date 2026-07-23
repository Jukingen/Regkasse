using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Maintenance;

public sealed class MaintenanceNotificationService : IMaintenanceNotificationService
{
    private static readonly TimeSpan DefaultForceLead = TimeSpan.FromHours(24);

    private readonly AppDbContext _db;

    public MaintenanceNotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MaintenanceNotificationDto> CreateAsync(
        string createdByUserId,
        CreateMaintenanceNotificationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateSchedule(request.ScheduledStartAt, request.ScheduledEndAt);
        var affected = NormalizeAffectedSystems(request.AffectedSystems);
        var title = request.Title.Trim();
        var message = request.Message.Trim();
        if (title.Length < 3 || message.Length < 3)
            throw new ArgumentException("Title or message is too short.");

        var now = DateTime.UtcNow;
        var entity = new MaintenanceNotification
        {
            Title = title,
            Message = message,
            ScheduledStartAt = EnsureUtc(request.ScheduledStartAt),
            ScheduledEndAt = EnsureUtc(request.ScheduledEndAt),
            Priority = Math.Clamp(request.Priority, 1, 5),
            IsMandatory = request.IsMandatory,
            IsForceDisplay = request.IsForceDisplay || request.IsMandatory,
            ForceDisplayFrom = request.ForceDisplayFrom.HasValue
                ? EnsureUtc(request.ForceDisplayFrom.Value)
                : EnsureUtc(request.ScheduledStartAt).Subtract(DefaultForceLead),
            AffectedSystems = affected,
            CreatedBy = createdByUserId,
            CreatedAt = now,
            UpdatedAt = now,
            Status = request.PublishImmediately
                ? MaintenanceNotificationStatuses.Published
                : MaintenanceNotificationStatuses.Draft,
            PublishedAt = request.PublishImmediately ? now : null,
        };

        _db.MaintenanceNotifications.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity, ack: null, now);
    }

    public async Task<MaintenanceNotificationDto?> UpdateAsync(
        Guid id,
        UpdateMaintenanceNotificationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.MaintenanceNotifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return null;

        if (entity.Status is MaintenanceNotificationStatuses.Completed
            or MaintenanceNotificationStatuses.Cancelled)
            throw new InvalidOperationException("Cannot update a completed or cancelled notification.");

        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (title.Length < 3)
                throw new ArgumentException("Title is too short.");
            entity.Title = title;
        }

        if (request.Message is not null)
        {
            var message = request.Message.Trim();
            if (message.Length < 3)
                throw new ArgumentException("Message is too short.");
            entity.Message = message;
        }

        if (request.ScheduledStartAt.HasValue)
            entity.ScheduledStartAt = EnsureUtc(request.ScheduledStartAt.Value);
        if (request.ScheduledEndAt.HasValue)
            entity.ScheduledEndAt = EnsureUtc(request.ScheduledEndAt.Value);

        ValidateSchedule(entity.ScheduledStartAt, entity.ScheduledEndAt);

        if (request.Priority.HasValue)
            entity.Priority = Math.Clamp(request.Priority.Value, 1, 5);
        if (request.IsMandatory.HasValue)
            entity.IsMandatory = request.IsMandatory.Value;
        if (request.IsForceDisplay.HasValue)
            entity.IsForceDisplay = request.IsForceDisplay.Value;
        if (entity.IsMandatory)
            entity.IsForceDisplay = true;

        if (request.ForceDisplayFrom.HasValue)
            entity.ForceDisplayFrom = EnsureUtc(request.ForceDisplayFrom.Value);
        else if (request.ScheduledStartAt.HasValue && entity.ForceDisplayFrom is null)
            entity.ForceDisplayFrom = entity.ScheduledStartAt.Subtract(DefaultForceLead);

        if (request.AffectedSystems is not null)
            entity.AffectedSystems = NormalizeAffectedSystems(request.AffectedSystems);

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity, ack: null, DateTime.UtcNow);
    }

    public async Task<MaintenanceNotificationDto?> PublishAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.MaintenanceNotifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return null;

        if (entity.Status is not MaintenanceNotificationStatuses.Draft
            and not MaintenanceNotificationStatuses.Cancelled)
            throw new InvalidOperationException("Only draft or cancelled notifications can be published.");

        var now = DateTime.UtcNow;
        entity.Status = MaintenanceNotificationStatuses.Published;
        entity.PublishedAt = now;
        entity.UpdatedAt = now;
        entity.ForceDisplayFrom ??= entity.ScheduledStartAt.Subtract(DefaultForceLead);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity, ack: null, now);
    }

    public async Task<MaintenanceNotificationDto?> CancelAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.MaintenanceNotifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return null;

        if (entity.Status is MaintenanceNotificationStatuses.Completed)
            throw new InvalidOperationException("Cannot cancel a completed notification.");

        var now = DateTime.UtcNow;
        entity.Status = MaintenanceNotificationStatuses.Cancelled;
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity, ack: null, now);
    }

    public async Task<MaintenanceNotificationDto?> MarkInProgressAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.MaintenanceNotifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return null;

        if (entity.Status is not MaintenanceNotificationStatuses.Published)
            throw new InvalidOperationException("Only published notifications can move to InProgress.");

        var now = DateTime.UtcNow;
        entity.Status = MaintenanceNotificationStatuses.InProgress;
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity, ack: null, now);
    }

    public async Task<MaintenanceNotificationDto?> CompleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.MaintenanceNotifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return null;

        if (entity.Status is not MaintenanceNotificationStatuses.Published
            and not MaintenanceNotificationStatuses.InProgress)
            throw new InvalidOperationException("Only published or in-progress notifications can be completed.");

        var now = DateTime.UtcNow;
        entity.Status = MaintenanceNotificationStatuses.Completed;
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity, ack: null, now);
    }

    public async Task<MaintenanceNotificationListResponseDto> ListAsync(
        string? status,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);

        var query = _db.MaintenanceNotifications.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            if (!MaintenanceNotificationStatuses.IsValid(normalized))
                throw new ArgumentException("Invalid status filter.");
            query = query.Where(n => n.Status == normalized);
        }

        query = query
            .OrderByDescending(n => n.Priority)
            .ThenBy(n => n.ScheduledStartAt);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query.Skip(offset).Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        return new MaintenanceNotificationListResponseDto
        {
            Total = total,
            Items = rows.Select(r => Map(r, ack: null, now)).ToList(),
        };
    }

    public async Task<IReadOnlyList<MaintenanceNotificationDto>> GetActiveForUserAsync(
        string userId,
        string surface,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.MaintenanceNotifications.AsNoTracking()
            .Where(n =>
                (n.Status == MaintenanceNotificationStatuses.Published
                 || n.Status == MaintenanceNotificationStatuses.InProgress)
                && n.ScheduledEndAt > now)
            .OrderByDescending(n => n.Priority)
            .ThenBy(n => n.ScheduledStartAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ids = rows.Select(r => r.Id).ToList();
        var acks = await _db.MaintenanceNotificationAcknowledgments.AsNoTracking()
            .Where(a => a.UserId == userId && ids.Contains(a.NotificationId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var ackById = acks.ToDictionary(a => a.NotificationId);

        var result = new List<MaintenanceNotificationDto>();
        foreach (var row in rows)
        {
            if (!MaintenanceAffectedSystems.AffectsSurface(row.AffectedSystems, surface))
                continue;

            ackById.TryGetValue(row.Id, out var ack);
            var dto = Map(row, ack, now);

            // Hide dismissed notices unless force-display / mandatory is in effect.
            if (ack?.IsDismissed == true && !dto.EffectiveForceDisplay)
                continue;

            result.Add(dto);
        }

        return result;
    }

    public async Task<MaintenanceNotificationDto?> AcknowledgeAsync(
        Guid id,
        string userId,
        AcknowledgeMaintenanceNotificationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.MaintenanceNotifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return null;

        if (!MaintenanceNotificationStatuses.IsClientVisible(entity.Status))
            throw new InvalidOperationException("Notification is not active.");

        var now = DateTime.UtcNow;
        var effectiveForce = ComputeEffectiveForceDisplay(entity, now);

        var ack = await _db.MaintenanceNotificationAcknowledgments
            .FirstOrDefaultAsync(a => a.NotificationId == id && a.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (ack is null)
        {
            ack = new MaintenanceNotificationAcknowledgment
            {
                NotificationId = id,
                UserId = userId,
            };
            _db.MaintenanceNotificationAcknowledgments.Add(ack);
        }

        if (request.MarkRead && !ack.IsRead)
        {
            ack.IsRead = true;
            ack.ReadAt = now;
        }

        var canDismiss = !entity.IsMandatory && !effectiveForce;
        if (request.Dismiss && canDismiss)
        {
            ack.IsDismissed = true;
            ack.DismissedAt = now;
        }
        else if (request.Dismiss && !canDismiss)
        {
            // Force/mandatory: allow read tracking but keep visible.
            if (!ack.IsRead)
            {
                ack.IsRead = true;
                ack.ReadAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(entity, ack, now);
    }

    private static MaintenanceNotificationDto Map(
        MaintenanceNotification entity,
        MaintenanceNotificationAcknowledgment? ack,
        DateTime utcNow)
    {
        var effectiveForce = ComputeEffectiveForceDisplay(entity, utcNow);
        var canDismiss = !entity.IsMandatory && !effectiveForce;
        return new MaintenanceNotificationDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Message = entity.Message,
            ScheduledStartAt = entity.ScheduledStartAt,
            ScheduledEndAt = entity.ScheduledEndAt,
            Status = entity.Status,
            Priority = entity.Priority,
            IsMandatory = entity.IsMandatory,
            IsForceDisplay = entity.IsForceDisplay,
            ForceDisplayFrom = entity.ForceDisplayFrom,
            AffectedSystems = entity.AffectedSystems,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt,
            PublishedAt = entity.PublishedAt,
            EffectiveForceDisplay = effectiveForce,
            CanDismiss = canDismiss,
            IsDismissedByCurrentUser = ack?.IsDismissed == true,
            IsReadByCurrentUser = ack?.IsRead == true,
        };
    }

    internal static bool ComputeEffectiveForceDisplay(MaintenanceNotification entity, DateTime utcNow)
    {
        if (entity.IsMandatory || entity.IsForceDisplay)
            return true;

        var forceFrom = entity.ForceDisplayFrom
            ?? entity.ScheduledStartAt.Subtract(DefaultForceLead);
        return utcNow >= forceFrom;
    }

    private static void ValidateSchedule(DateTime start, DateTime end)
    {
        start = EnsureUtc(start);
        end = EnsureUtc(end);
        if (end <= start)
            throw new ArgumentException("ScheduledEndAt must be after ScheduledStartAt.");
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private static string NormalizeAffectedSystems(string raw)
    {
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return MaintenanceAffectedSystems.All;

        var normalized = new List<string>();
        foreach (var part in parts)
        {
            if (string.Equals(part, MaintenanceAffectedSystems.All, StringComparison.OrdinalIgnoreCase))
                return MaintenanceAffectedSystems.All;
            if (string.Equals(part, MaintenanceAffectedSystems.Pos, StringComparison.OrdinalIgnoreCase))
                normalized.Add(MaintenanceAffectedSystems.Pos);
            else if (string.Equals(part, MaintenanceAffectedSystems.Fa, StringComparison.OrdinalIgnoreCase))
                normalized.Add(MaintenanceAffectedSystems.Fa);
            else if (string.Equals(part, MaintenanceAffectedSystems.Api, StringComparison.OrdinalIgnoreCase))
                normalized.Add(MaintenanceAffectedSystems.Api);
            else
                throw new ArgumentException($"Invalid affected system: {part}");
        }

        return string.Join(',', normalized.Distinct(StringComparer.Ordinal));
    }
}
