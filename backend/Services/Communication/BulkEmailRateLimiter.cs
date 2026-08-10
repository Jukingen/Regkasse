using Microsoft.Extensions.Caching.Memory;

namespace KasseAPI_Final.Services.Communication;

/// <summary>Process-local cap: max 100 bulk emails per rolling minute.</summary>
public sealed class BulkEmailRateLimiter : IBulkEmailRateLimiter
{
    public const int MaxEmailsPerMinute = 100;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private const string CacheKey = "bulk-email:window";

    private readonly IMemoryCache _cache;

    public BulkEmailRateLimiter(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string? TryAcquireOrError(int emailCount)
    {
        if (emailCount <= 0)
            return null;

        var counter = _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Window;
            return new AttemptCounter();
        })!;

        var next = counter.Add(emailCount);
        if (next > MaxEmailsPerMinute)
        {
            // Roll back this acquire so callers can retry later with a smaller batch.
            counter.Add(-emailCount);
            return $"Rate limit exceeded: max {MaxEmailsPerMinute} bulk emails per minute.";
        }

        return null;
    }

    private sealed class AttemptCounter
    {
        private int _count;

        public int Add(int delta) => Interlocked.Add(ref _count, delta);
    }
}
