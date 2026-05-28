using KasseAPI_Final.Models;
using Microsoft.Extensions.DependencyInjection;

namespace KasseAPI_Final.Services.Activity;

/// <summary>Convenience publisher used by controllers and background workers.</summary>
public interface IActivityEventPublisher
{
    Task TryPublishAsync(
        Guid tenantId,
        ActivityEventType type,
        object? metadata = null,
        string? actorUserId = null,
        string? dedupKey = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Fire-and-forget wrapper so primary flows never fail on activity feed errors.</summary>
public sealed class ActivityEventRecorder : IActivityEventPublisher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityEventRecorder> _logger;

    public ActivityEventRecorder(
        IServiceScopeFactory scopeFactory,
        ILogger<ActivityEventRecorder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task TryPublishAsync(
        Guid tenantId,
        ActivityEventType type,
        object? metadata = null,
        string? actorUserId = null,
        string? dedupKey = null,
        CancellationToken cancellationToken = default)
    {
        var request = ActivityEventPublishBuilder.FromMetadata(
            tenantId,
            type,
            metadata,
            actorUserId,
            dedupKey);
        return TryPublishAsync(request, cancellationToken);
    }

    public async Task TryPublishAsync(
        ActivityEventPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var activity = scope.ServiceProvider.GetRequiredService<IActivityEventService>();
            await activity.PublishAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Activity event publish failed Type={Type} TenantId={TenantId}",
                request.Type,
                request.TenantId);
        }
    }
}
