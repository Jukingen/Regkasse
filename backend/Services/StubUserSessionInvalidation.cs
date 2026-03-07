namespace KasseAPI_Final.Services;

/// <summary>
/// Session invalidation stub – RefreshToken tablosu olmadan sadece log.
/// JWT tarafında anında iptal yok; token süresi dolana kadar geçerli kalır.
/// </summary>
public class StubUserSessionInvalidation : IUserSessionInvalidation
{
    private readonly ILogger<StubUserSessionInvalidation> _logger;

    public StubUserSessionInvalidation(ILogger<StubUserSessionInvalidation> logger)
    {
        _logger = logger;
    }

    public Task InvalidateSessionsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Session invalidation requested for user {UserId} (stub: no refresh tokens stored yet)", userId);
        return Task.CompletedTask;
    }
}
