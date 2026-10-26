namespace KasseAPI_Final.Services.DemoRequests;

/// <summary>
/// In-process cap for anonymous demo requests.
/// Hardcoded on purpose: do not add an appsettings key for this public form.
/// </summary>
public sealed class DemoRequestRateLimiter
{
    /// <summary>Three accepted submissions per client address per rolling hour.</summary>
    public const int MaxRequestsPerIpPerHour = 3;

    public const string RateLimitedCode = "DEMO_RATE_LIMITED";

    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly object _gate = new();
    private readonly Dictionary<string, List<DateTime>> _hits = new(StringComparer.Ordinal);
    private readonly Func<DateTime> _utcNow;

    public DemoRequestRateLimiter(Func<DateTime>? utcNow = null)
    {
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>Returns false when the address is already at the cap. Does not record a rejected hit.</summary>
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
            if (hits.Count >= MaxRequestsPerIpPerHour)
                return false;

            hits.Add(now);
            return true;
        }
    }
}
