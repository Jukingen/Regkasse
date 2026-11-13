using KasseAPI_Final.Services;
using Microsoft.Extensions.Caching.Memory;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Records API activity on <c>auth_sessions.last_activity_at_utc</c> for the JWT <c>sid</c> claim.
/// Idle logout (default 30 minutes) and the pre-timeout warning (default 5 minutes) are enforced
/// in the Admin UI via activity listeners; this middleware keeps server-side last-active accurate
/// for device/session management. Touches are throttled to once per minute per session.
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
                        /* non-fatal — idle timeout must not break API calls */
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
