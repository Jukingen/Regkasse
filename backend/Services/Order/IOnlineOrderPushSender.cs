namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Mobile push for online-order customers. No FCM/APNs provider is wired yet —
/// <see cref="LoggingOnlineOrderPushSender"/> records intents until a real provider is registered.
/// </summary>
public interface IOnlineOrderPushSender
{
    Task<bool> TrySendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}

public sealed class LoggingOnlineOrderPushSender : IOnlineOrderPushSender
{
    private readonly ILogger<LoggingOnlineOrderPushSender> _logger;

    public LoggingOnlineOrderPushSender(ILogger<LoggingOnlineOrderPushSender> logger)
    {
        _logger = logger;
    }

    public Task<bool> TrySendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
            return Task.FromResult(false);

        _logger.LogInformation(
            "Online order push queued (provider not configured): tokenPrefix={TokenPrefix} title={Title} body={Body}",
            deviceToken.Length <= 8 ? deviceToken : deviceToken[..8],
            title,
            body);
        return Task.FromResult(true);
    }
}
