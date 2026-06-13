using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.Localization;
using KasseAPI_Final.Services.Localization;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Blocks authenticated API calls when the user must change their password,
/// except password change and session teardown endpoints.
/// </summary>
public sealed class MustChangePasswordMiddleware
{
    private static readonly HashSet<string> AllowedPathPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/logout",
        "/api/auth/me",
        "/api/health",
        "/swagger",
        "/api/usermanagement/me/password",
        "/api/UserManagement/me/password",
    };

    /// <summary>Test hook and documentation for exempt API paths while password change is pending.</summary>
    public static bool IsExemptPath(string path) => IsAllowedPath(path);

    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db, IApiMessageLocalizer messages)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (!IsAllowedPath(path))
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirstValue("sub");
                if (!string.IsNullOrEmpty(userId))
                {
                    var mustChange = await db.Users.AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => u.MustChangePasswordOnNextLogin)
                        .FirstOrDefaultAsync(context.RequestAborted)
                        .ConfigureAwait(false);

                    if (mustChange)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            message = messages.Get(ApiMessageKeys.PasswordChangeRequired),
                            code = "PASSWORD_CHANGE_REQUIRED",
                        }).ConfigureAwait(false);
                        return;
                    }
                }
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool IsAllowedPath(string path)
    {
        foreach (var prefix in AllowedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
