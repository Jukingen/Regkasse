namespace KasseAPI_Final.Configuration;

/// <summary>
/// StackExchange.Redis connection settings for <see cref="Services.Cache.ICacheService"/>.
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>Redis endpoint(s), e.g. <c>localhost:6379</c> or cluster DNS.</summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Key prefix for this environment (e.g. <c>Regkasse_Dev</c> / <c>Regkasse_Prod</c>)
    /// so Dev and Prod (or multiple apps) can share a Redis instance safely.
    /// </summary>
    public string InstanceName { get; set; } = "Regkasse";
}
