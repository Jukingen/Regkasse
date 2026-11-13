using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Polls time-bound user permission overrides: expiring-soon notifications and expiry session invalidation.
/// </summary>
public sealed class TemporaryPermissionExpiryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TemporaryPermissionsOptions> _options;
    private readonly ILogger<TemporaryPermissionExpiryHostedService> _logger;

    public TemporaryPermissionExpiryHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TemporaryPermissionsOptions> options,
        ILogger<TemporaryPermissionExpiryHostedService> logger)
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
            try
            {
                await ProcessAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Temporary permission expiry job failed");
            }

            var minutes = Math.Clamp(_options.CurrentValue.PollIntervalMinutes, 1, 24 * 60);
            await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activity = scope.ServiceProvider.GetRequiredService<IActivityEventPublisher>();
        var sessions = scope.ServiceProvider.GetRequiredService<IUserSessionInvalidation>();
        var time = scope.ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
        var now = time.GetUtcNow().UtcDateTime;
        var soonHours = Math.Max(1, _options.CurrentValue.ExpiringSoonHours);
        var soonThreshold = now.AddHours(soonHours);

        var expiringSoon = await db.UserPermissionOverrides
            .Where(o => o.ExpiresAt != null
                        && o.ExpiresAt > now
                        && o.ExpiresAt <= soonThreshold
                        && o.ExpiringNotifiedAt == null
                        && (o.ValidFrom == null || o.ValidFrom <= now))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in expiringSoon)
        {
            row.ExpiringNotifiedAt = now;
            if (row.TenantId.HasValue)
            {
                await activity.TryPublishAsync(
                    row.TenantId.Value,
                    ActivityEventType.UserPermissionOverrideExpiringSoon,
                    new
                    {
                        OverrideId = row.Id.ToString(),
                        row.UserId,
                        row.Permission,
                        row.ExpiresAt,
                        Message = $"Temporary permission {row.Permission} expires soon.",
                    },
                    actorUserId: null,
                    dedupKey: $"perm-override-expiring:{row.Id}",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        var expired = await db.UserPermissionOverrides
            .Where(o => o.ExpiresAt != null
                        && o.ExpiresAt <= now
                        && o.ExpiredProcessedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var invalidatedUsers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in expired)
        {
            row.ExpiredProcessedAt = now;
            if (row.TenantId.HasValue)
            {
                await activity.TryPublishAsync(
                    row.TenantId.Value,
                    ActivityEventType.UserPermissionOverrideExpired,
                    new
                    {
                        OverrideId = row.Id.ToString(),
                        row.UserId,
                        row.Permission,
                        row.ExpiresAt,
                        Message = $"Temporary permission {row.Permission} expired.",
                    },
                    actorUserId: null,
                    dedupKey: $"perm-override-expired:{row.Id}",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            invalidatedUsers.Add(row.UserId);
        }

        if (expiringSoon.Count > 0 || expired.Count > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var userId in invalidatedUsers)
        {
            try
            {
                await sessions.InvalidateSessionsForUserAsync(userId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate sessions for user {UserId} after permission expiry", userId);
            }
        }

        if (expiringSoon.Count > 0 || expired.Count > 0)
        {
            _logger.LogInformation(
                "Temporary permission job: expiringSoon={Expiring} expired={Expired}",
                expiringSoon.Count,
                expired.Count);
        }
    }
}
