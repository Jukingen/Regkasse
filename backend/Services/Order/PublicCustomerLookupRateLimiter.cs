namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Tight cap for the anonymous customer dashboard. Hardcoded so the public lookup
/// cannot be widened from configuration.
/// </summary>
public sealed class PublicCustomerLookupRateLimiter
{
    public const int MaxRequestsPerWindow = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private readonly Dictionary<string, List<DateTime>> _hits = new(StringComparer.Ordinal);
    private readonly Func<DateTime> _utcNow;

    public PublicCustomerLookupRateLimiter(Func<DateTime>? utcNow = null)
    {
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>Returns false when the client is already at the cap. Rejected calls are not stored.</summary>
    public bool TryConsume(string clientKey)
    {
        var key = string.IsNullOrWhiteSpace(clientKey) ? "unknown" : clientKey.Trim();
        var now = _utcNow();
        var cutoff = now - Window;

        lock (_gate)
        {
            if (!_hits.TryGetValue(key, out var hits))
            {
                hits = [];
                _hits[key] = hits;
            }

            hits.RemoveAll(stamp => stamp <= cutoff);
            if (hits.Count >= MaxRequestsPerWindow)
                return false;

            hits.Add(now);
            return true;
        }
    }
}
