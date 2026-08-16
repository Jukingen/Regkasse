namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>Process-wide fiskaly bearer cache (typed HttpClient is transient).</summary>
public sealed class FiskalyAccessTokenCache
{
    private readonly object _gate = new();
    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public bool TryGet(out string token, out DateTimeOffset expiresAt)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(_accessToken)
                && DateTimeOffset.UtcNow < _expiresAt.AddMinutes(-1))
            {
                token = _accessToken;
                expiresAt = _expiresAt;
                return true;
            }

            token = string.Empty;
            expiresAt = DateTimeOffset.MinValue;
            return false;
        }
    }

    public void Set(string token, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        lock (_gate)
        {
            _accessToken = token;
            _expiresAt = expiresAt;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _accessToken = null;
            _expiresAt = DateTimeOffset.MinValue;
        }
    }
}
