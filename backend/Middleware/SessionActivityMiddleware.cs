using KasseAPI_Final.Services;
using Microsoft.Extensions.Caching.Memory;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Throttled updates to <c>auth_sessions.last_activity_at_utc</c> for the current JWT session (<c>sid</c> claim).
/// Skips static assets and endpoints that already touch activity explicitly.
/// </summary>
public sealed class SessionActivityMiddleware
{
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;

    public SessionActivityMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context, IUserSessionService sessionService)
    {
        if (context.User.Identity?.IsAuthenticated == true && IsActivityEndpoint(context))
        {
            var sid = context.User.FindFirst("sid")?.Value;
            if (Guid.TryParse(sid, out var sessionId))
            {
                var cacheKey = $"session-touch:{sessionId:N}";
                if (!_cache.TryGetValue(cacheKey, out _))
                {
                    _cache.Set(cacheKey, true, TouchInterval);
                    try
                    {
                        await sessionService
                            .TouchSessionActivityAsync(sessionId, context.RequestAborted)
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        /* non-fatal */
                    }
                }
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns true when the request should count as user/API activity (not static or explicit refresh).
    /// </summary>
    internal static bool IsActivityEndpoint(HttpContext context)
    {
        if (context.Request.Method is "OPTIONS" or "HEAD")
            return false;

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.Length == 0)
            return false;

        var lower = path.ToLowerInvariant();

        if (lower.StartsWith("/_next", StringComparison.Ordinal))
            return false;

        if (lower.Contains('.', StringComparison.Ordinal))
            return false;

        // refresh-session / heartbeat already update last activity
        if (lower.StartsWith("/api/auth/refresh", StringComparison.Ordinal)
            || lower == "/api/user/sessions/heartbeat")
            return false;

        return lower.StartsWith("/api/", StringComparison.Ordinal);
    }
}
