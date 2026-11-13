namespace KasseAPI_Final.Services.Push;

/// <summary>Staff / FA mobile push payload (provider-agnostic).</summary>
public sealed class PushNotification
{
    public required string UserId { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public IReadOnlyDictionary<string, string>? Data { get; init; }
}

/// <summary>
/// Mobile push delivery for authenticated users. No FCM/APNs/Expo provider is wired yet —
/// <see cref="LoggingPushNotificationService"/> records intents until a real provider is registered.
/// </summary>
public interface IPushNotificationService
{
    Task<bool> SendAsync(PushNotification notification, CancellationToken cancellationToken = default);
}

public sealed class LoggingPushNotificationService : IPushNotificationService
{
    private readonly ILogger<LoggingPushNotificationService> _logger;

    public LoggingPushNotificationService(ILogger<LoggingPushNotificationService> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(PushNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.UserId))
            return Task.FromResult(false);

        _logger.LogInformation(
            "Staff push queued (provider not configured): userId={UserId} title={Title} body={Body} dataKeys={DataKeys}",
            notification.UserId,
            notification.Title,
            notification.Body,
            notification.Data is null ? 0 : notification.Data.Count);
        return Task.FromResult(true);
    }
}
