using Microsoft.Extensions.Caching.Memory;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed class TenantLicenseExtensionRateLimiter : ITenantLicenseExtensionRateLimiter
{
    private const int MaxAttemptsPerWindow = 10;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private readonly IMemoryCache _cache;

    public TenantLicenseExtensionRateLimiter(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string? TryAcquireOrError(string? actorUserId, Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            return null;

        var cacheKey = $"tenant-license-extend:{actorUserId}:{tenantId:D}";
        var counter = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Window;
            return new AttemptCounter();
        })!;

        if (counter.Increment() > MaxAttemptsPerWindow)
        {
            return "Too many license extension attempts. Please try again later.";
        }

        return null;
    }

    private sealed class AttemptCounter
    {
        private int _count;

        public int Increment() => Interlocked.Increment(ref _count);
    }
}
