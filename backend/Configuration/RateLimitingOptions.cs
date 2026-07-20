namespace KasseAPI_Final.Configuration;

/// <summary>
/// Global HTTP rate limiting (IP + path). Disabled by default; enable in Production.
/// Does not alter API contracts — excess traffic receives HTTP 429.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>When false, middleware is a no-op (except Development, which always skips).</summary>
    public bool Enabled { get; set; }

    /// <summary>Max requests per client IP and path within <see cref="WindowSeconds"/>.</summary>
    public int Limit { get; set; } = 100;

    /// <summary>Sliding fixed window length in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;
}
