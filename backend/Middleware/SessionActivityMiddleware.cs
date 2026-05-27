using KasseAPI_Final.Services;
using Microsoft.Extensions.Caching.Memory;

namespace KasseAPI_Final.Middleware;

/// <summary>Throttled updates to auth_sessions.last_activity_at_utc for the current JWT session.</summary>
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

    public async Task InvokeAsync(HttpContext context, IUserSessionService sessions)
    {
        if (context.User.Identity?.IsAuthenticated == true)
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
                        await sessions.TouchSessionActivityAsync(sessionId, context.RequestAborted).ConfigureAwait(false);
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
}
