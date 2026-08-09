namespace KasseAPI_Final.Services.Caching;

/// <summary>
/// Centralized cache abstraction for domain read-through caching (license, products, etc.).
/// Production uses Redis (<see cref="RedisCacheService"/>); Development uses
/// <see cref="MemoryCacheService"/> unless Redis is explicitly enabled.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes all keys that start with <paramref name="prefix"/> (ordinal).</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>Removes every key tracked by this cache instance.</summary>
    Task ClearAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
