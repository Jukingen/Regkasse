using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Non-breaking global rate limiter: HTTP 429 when a client exceeds configured limits.
/// Skips Development, disabled config, and health/swagger/metrics endpoints.
/// </summary>
public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<RateLimitingOptions> _options;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        ILogger<RateLimitingMiddleware> logger,
        IHostEnvironment environment,
        IOptionsMonitor<RateLimitingOptions> options)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
        _environment = environment;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_environment.IsDevelopment())
        {
            await _next(context);
            return;
        }

        var options = _options.CurrentValue;
        if (!options.Enabled || options.Limit <= 0 || options.WindowSeconds <= 0)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (IsExemptPath(path))
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"ratelimit:{ip}:{path}";
        var window = TimeSpan.FromSeconds(options.WindowSeconds);

        var counter = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return new RequestCounter();
        })!;

        var count = counter.Increment();
        if (count > options.Limit)
        {
            _logger.LogWarning(
                "Rate limit exceeded for IP {Ip} on path {Path} (count={Count}, limit={Limit})",
                ip,
                path,
                count,
                options.Limit);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = options.WindowSeconds.ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = "Too many requests. Please try again later.",
                retryAfter = options.WindowSeconds
            });
            return;
        }

        await _next(context);
    }

    internal static bool IsExemptPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RequestCounter
    {
        private int _count;

        public int Increment() => Interlocked.Increment(ref _count);
    }
}
