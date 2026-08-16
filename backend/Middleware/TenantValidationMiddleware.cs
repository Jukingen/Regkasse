using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Fail-closed ambient tenant gate: tenant-scoped API paths require
/// <see cref="ICurrentTenantAccessor.TenantId"/> (HTTP 404 when unset).
/// <para>
/// Exemptions:
/// </para>
/// <list type="bullet">
/// <item><description>PublicPaths — unauthenticated / identity surfaces (<c>/api/auth/*</c>, health, swagger).</description></item>
/// <item><description>SuperAdminPlatformPathPrefixes — <strong>only when</strong> the caller is
/// authenticated SuperAdmin. These routes target tenants by route/body id (or are deployment-wide),
/// not by ambient mandant. Non–SuperAdmin callers still need ambient tenant.</description></item>
/// </list>
/// </summary>
public class TenantValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantValidationMiddleware> _logger;

    // Public / identity endpoints that don't require ambient mandant (platform hosts leave
    // tenant unset until JWT bind). Prefix `/api/auth/` covers login, refresh, 2FA, GET /me,
    // logout, and forgot-* so FA bootstrap is not 404'd when JWT tenant_id is missing or
    // still unbound. Trailing slash keeps this segment-safe (`/api/authfoo` does not match).
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/",
        "/api/csrf",
        "/api/health",
        "/api/public",
        "/health",
        "/metrics",
        "/swagger",
        "/swagger/index.html",
    };

    /// <summary>
    /// Prefixes (segment-safe) exempt from ambient tenant <strong>for SuperAdmin only</strong>.
    /// Keep this list minimal — mandant data APIs (<c>/api/admin/products</c>, etc.) must NOT be listed.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><c>/api/admin/tenants</c> — SaaS tenant CRUD + impersonate; target = route <c>tenantId</c>.</description></item>
    /// <item><description><c>/api/admin/billing</c> — license sales (SystemCritical); target tenant in body/route.</description></item>
    /// <item><description><c>/api/admin/cache</c> — deployment/tenant cache clear; optional body tenantId.</description></item>
    /// <item><description><c>/api/admin/support</c> — Super Admin ticket inbox (all tenants).</description></item>
    /// <item><description><c>/api/admin/trials</c> — SaaS trial dashboard / conversion (SystemCritical).</description></item>
    /// <item><description><c>/api/admin/fiskaly</c> — Super Admin may set a global Fiskaly overlay without ambient tenant. Mandanten-Admin still needs ambient tenant (tenant overlay).</description></item>
    /// </list>
    /// Exact path <c>/api/tenants/switcher</c> is also exempt for SuperAdmin (membership-wide list;
    /// <c>/api/tenants/current</c> still requires ambient).
    /// </remarks>
    private static readonly string[] SuperAdminPlatformPathPrefixes =
    [
        "/api/admin/tenants",
        "/api/admin/billing",
        "/api/admin/cache",
        "/api/admin/support",
        "/api/admin/trials",
        "/api/admin/fiskaly",
    ];

    private static readonly string[] SuperAdminExactExemptPaths =
    [
        "/api/tenants/switcher",
    ];

    public TenantValidationMiddleware(RequestDelegate next, ILogger<TenantValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenantAccessor tenantAccessor)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        if (IsPublicPath(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // SuperAdmin platform SaaS routes may run without ambient mandant.
        // Non–SuperAdmin on the same URL still require ambient tenant (fail-closed).
        if (IsSuperAdmin(context.User) && IsSuperAdminPlatformExemptPath(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (tenantAccessor.TenantId == null)
        {
            _logger.LogWarning("Request to {Path} rejected: No tenant context", path);

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";

            var error = new
            {
                error = "Not Found",
                message = "The requested resource could not be found",
                status = 404,
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(error)).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>True when path is a documented SuperAdmin platform exemption (role not checked here).</summary>
    public static bool IsSuperAdminPlatformExemptPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        foreach (var exact in SuperAdminExactExemptPaths)
        {
            if (path.Equals(exact, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var prefix in SuperAdminPlatformPathPrefixes)
        {
            if (MatchesPathPrefix(path, prefix))
                return true;
        }

        return false;
    }

    private static bool IsPublicPath(string path) =>
        PublicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Segment-safe prefix: <c>/api/admin/tenants</c> matches itself and
    /// <c>/api/admin/tenants/…</c>, but not <c>/api/admin/tenantsfoo</c>.
    /// </summary>
    internal static bool MatchesPathPrefix(string path, string prefix)
    {
        if (path.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            return true;

        return path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuperAdmin(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        if (user.IsInRole(Roles.SuperAdmin))
            return true;

        foreach (var claim in user.FindAll("role"))
        {
            if (string.Equals(claim.Value, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
