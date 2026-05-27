using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Activity;

public sealed class ActivityEventService : IActivityEventService
{
    private readonly AppDbContext _db;
    private readonly IActivityEventEmailNotifier _email;
    private readonly IActivityEventWebhookNotifier _webhook;
    private readonly INotificationConfigService _notificationConfig;
    private readonly IActivityStreamHub _streamHub;
    private readonly ActivityNotificationOptions _options;
    private readonly ILogger<ActivityEventService> _logger;

    public ActivityEventService(
        AppDbContext db,
        IActivityEventEmailNotifier email,
        IActivityEventWebhookNotifier webhook,
        INotificationConfigService notificationConfig,
        IActivityStreamHub streamHub,
        IOptions<ActivityNotificationOptions> options,
        ILogger<ActivityEventService> logger)
    {
        _db = db;
        _email = email;
        _webhook = webhook;
        _notificationConfig = notificationConfig;
        _streamHub = streamHub;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ActivityEvent> PublishAsync(
        ActivityEventPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var severity = ActivitySeverityNames.NormalizeOrDefault(
            request.Severity,
            ActivityEventSeverityRules.DefaultFor(request.Type));

        string? metadataJson = null;
        if (request.Metadata is { Count: > 0 })
            metadataJson = JsonSerializer.Serialize(request.Metadata);

        var actorName = request.ActorName
            ?? await ResolveActorNameAsync(request.ActorUserId, cancellationToken).ConfigureAwait(false);

        ActivityEvent entity;
        if (!string.IsNullOrWhiteSpace(request.DedupKey))
        {
            var existing = await _db.ActivityEvents
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == request.TenantId && e.DedupKey == request.DedupKey)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (existing != null)
            {
                existing.Title = request.Title;
                existing.Description = request.Description;
                existing.Severity = severity;
                existing.Type = request.Type;
                existing.CreatedAtUtc = now;
                existing.ActorUserId = request.ActorUserId ?? existing.ActorUserId;
                existing.ActorName = actorName ?? existing.ActorName;
                existing.EntityType = request.EntityType ?? existing.EntityType;
                existing.EntityId = request.EntityId ?? existing.EntityId;
                existing.MetadataJson = metadataJson ?? existing.MetadataJson;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                entity = existing;
            }
            else
            {
                entity = await InsertAsync(request, severity, actorName, metadataJson, now, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        else
        {
            entity = await InsertAsync(request, severity, actorName, metadataJson, now, cancellationToken)
                .ConfigureAwait(false);
        }

        var tenantConfig = await _notificationConfig.GetAsync(request.TenantId, cancellationToken).ConfigureAwait(false);

        if (NotificationConfigEvaluator.ShouldDeliverInApp(tenantConfig, entity.Type, entity.Severity))
        {
            var streamDto = ActivityEventMapper.ToDto(entity, isRead: false);
            _streamHub.Publish(request.TenantId, streamDto);
        }

        if (!request.SkipOutboundDelivery)
            _ = DeliverOutboundFireAndForgetAsync(entity, tenantConfig);

        return entity;
    }

    public async Task<ActivitiesListResponseDto> ListAsync(
        string userId,
        Guid tenantId,
        int limit,
        int offset,
        string? severityFilter,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);

        var query = _db.ActivityEvents.AsNoTracking().Where(e => e.TenantId == tenantId);

        if (ActivitySeverityNames.TryNormalizeFilter(severityFilter, out var normalizedSeverity))
            query = query.Where(e => e.Severity == normalizedSeverity);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var events = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var eventIds = events.Select(e => e.Id).ToList();
        var reads = await _db.ActivityEventReads
            .AsNoTracking()
            .Where(r => r.UserId == userId && eventIds.Contains(r.ActivityEventId))
            .ToDictionaryAsync(r => r.ActivityEventId, r => r.ReadAtUtc, cancellationToken)
            .ConfigureAwait(false);

        return new ActivitiesListResponseDto
        {
            Items = events.Select(e =>
            {
                var isRead = reads.TryGetValue(e.Id, out var readAt);
                return ActivityEventMapper.ToDto(e, isRead, isRead ? readAt : null);
            }).ToList(),
            Total = total,
            Limit = limit,
            Offset = offset,
        };
    }

    public async Task<ActivitiesUnreadCountDto> GetUnreadCountAsync(
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var count = await (
            from e in _db.ActivityEvents.AsNoTracking()
            where e.TenantId == tenantId
            where !_db.ActivityEventReads.Any(r => r.ActivityEventId == e.Id && r.UserId == userId)
            select e.Id).CountAsync(cancellationToken).ConfigureAwait(false);

        return new ActivitiesUnreadCountDto { UnreadCount = count };
    }

    public async Task<ActivityDto?> MarkEventReadAsync(
        string userId,
        Guid tenantId,
        Guid activityId,
        CancellationToken cancellationToken = default)
    {
        var evt = await _db.ActivityEvents
            .FirstOrDefaultAsync(e => e.Id == activityId && e.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (evt == null)
            return null;

        var now = DateTime.UtcNow;
        var existing = await _db.ActivityEventReads
            .FirstOrDefaultAsync(r => r.ActivityEventId == activityId && r.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (existing == null)
        {
            _db.ActivityEventReads.Add(new ActivityEventRead
            {
                ActivityEventId = activityId,
                UserId = userId,
                ReadAtUtc = now,
            });
        }
        else
        {
            existing.ReadAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ActivityEventMapper.ToDto(evt, isRead: true, readAtUtc: now);
    }

    public async Task<int> MarkAllReadAsync(
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var unreadIds = await (
            from e in _db.ActivityEvents
            where e.TenantId == tenantId
            where !_db.ActivityEventReads.Any(r => r.ActivityEventId == e.Id && r.UserId == userId)
            select e.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (unreadIds.Count == 0)
            return 0;

        var existing = await _db.ActivityEventReads
            .Where(r => r.UserId == userId && unreadIds.Contains(r.ActivityEventId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingIds = existing.Select(r => r.ActivityEventId).ToHashSet();
        foreach (var read in existing)
            read.ReadAtUtc = now;

        foreach (var id in unreadIds.Where(id => !existingIds.Contains(id)))
        {
            _db.ActivityEventReads.Add(new ActivityEventRead
            {
                ActivityEventId = id,
                UserId = userId,
                ReadAtUtc = now,
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return unreadIds.Count;
    }

    public async Task<bool> DeleteAsync(
        Guid tenantId,
        Guid activityId,
        CancellationToken cancellationToken = default)
    {
        var evt = await _db.ActivityEvents
            .FirstOrDefaultAsync(e => e.Id == activityId && e.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (evt == null)
            return false;

        var retentionDays = Math.Max(1, _options.DeleteRetentionDays);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        if (evt.CreatedAtUtc > cutoff)
            return false;

        var reads = await _db.ActivityEventReads
            .Where(r => r.ActivityEventId == activityId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.ActivityEventReads.RemoveRange(reads);
        _db.ActivityEvents.Remove(evt);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var retentionDays = Math.Max(1, _options.EventRetentionDays);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        var expiredIds = await _db.ActivityEvents
            .IgnoreQueryFilters()
            .Where(e => e.CreatedAtUtc < cutoff)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (expiredIds.Count == 0)
            return 0;

        var reads = await _db.ActivityEventReads
            .IgnoreQueryFilters()
            .Where(r => expiredIds.Contains(r.ActivityEventId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.ActivityEventReads.RemoveRange(reads);

        var events = await _db.ActivityEvents
            .IgnoreQueryFilters()
            .Where(e => expiredIds.Contains(e.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.ActivityEvents.RemoveRange(events);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return events.Count;
    }

    private async Task<ActivityEvent> InsertAsync(
        ActivityEventPublishRequest request,
        string severity,
        string? actorName,
        string? metadataJson,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var entity = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Type = request.Type,
            Severity = severity,
            Title = request.Title,
            Description = request.Description,
            DedupKey = request.DedupKey,
            ActorUserId = request.ActorUserId,
            ActorName = actorName,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            MetadataJson = metadataJson,
            CreatedAtUtc = now,
        };

        _db.ActivityEvents.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    private async Task<string?> ResolveActorNameAsync(string? actorUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            return null;

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == actorUserId)
            .Select(u => new { u.FirstName, u.LastName, u.UserName })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (user == null)
            return null;

        var full = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(full) ? user.UserName : full;
    }

    private async Task DeliverOutboundFireAndForgetAsync(ActivityEvent entity, NotificationConfig tenantConfig)
    {
        try
        {
            await _email.TrySendAsync(entity, tenantConfig).ConfigureAwait(false);
            await _webhook.TrySendAsync(entity, tenantConfig).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activity outbound delivery failed EventId={EventId}", entity.Id);
        }
    }
}
