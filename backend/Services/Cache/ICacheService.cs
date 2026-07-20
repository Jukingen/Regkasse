namespace KasseAPI_Final.Services.Cache;

/// <summary>
/// Lightweight cache abstraction for read-through caching.
/// Default implementation is in-memory; Redis can replace it without call-site changes.
/// </summary>
public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null);

    Task RemoveAsync(string key);

    Task RemoveByPrefixAsync(string prefix);

    Task<bool> ExistsAsync(string key);
}
