namespace KasseAPI_Final.Configuration;

/// <summary>
/// StackExchange.Redis / <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> settings
/// for <see cref="Services.Caching.ICacheService"/>.
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// When true, register Redis-backed <see cref="Services.Caching.RedisCacheService"/>.
    /// When false (typical Development), use <see cref="Services.Caching.MemoryCacheService"/>.
    /// Production defaults to enabled unless explicitly set to false.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Redis endpoint(s), e.g. <c>localhost:6379</c> or cluster DNS.</summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Key prefix for this environment (e.g. <c>Regkasse_Dev</c> / <c>Regkasse_Prod</c>)
    /// so Dev and Prod (or multiple apps) can share a Redis instance safely.
    /// </summary>
    public string InstanceName { get; set; } = "Regkasse";
}
