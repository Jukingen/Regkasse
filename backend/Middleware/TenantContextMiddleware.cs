using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// After authentication, re-binds ambient tenant.
/// <list type="bullet">
/// <item><description>Development: when <see cref="SubdomainTenantProvider.DevTenantHeaderName"/> / <c>?tenant=</c> is present (and not platform <c>admin</c>), that override wins over JWT <strong>when it resolves</strong>. Unknown or inactive override falls through to JWT / SuperAdmin <c>dev</c> default instead of leaving ambient null (which would 404 every mandant API).</description></item>
/// <item><description>Development SuperAdmin without JWT/header: <see cref="ITenantContextService"/> prefers seeded <c>dev</c> (not silent Production defaults).</description></item>
/// <item><description>Production/Staging: authenticated requests use JWT <c>tenant_id</c> only — header/query are ignored; missing/invalid claim clears ambient tenant (fail-closed), including SuperAdmin.</description></item>
/// <item><description>When <see cref="AuthOptions.RequireTenantHostMatch"/> is enabled (non-Development): mandant subdomain / custom domain Host must match JWT <c>tenant_id</c> (shared platform hosts and SuperAdmin impersonation exempt). Mismatch → HTTP 403.</description></item>
/// </list>
/// Pipeline: runs immediately after <c>UseAuthentication</c> and before license / authorization gates.
/// </summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public TenantContextMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContextService tenantContextService,
        ICurrentTenantAccessor tenantAccessor,
        IOptions<AuthOptions> authOptions,
        ILogger<TenantContextMiddleware> logger)
    {
        // Development: resolved header/query wins over JWT (local mandant switching).
        // Unresolved override must not short-circuit — FA always sends X-Tenant-Id:dev after
        // SuperAdmin/platform login; if that slug is missing/inactive, JWT still has to bind
        // or TenantValidationMiddleware 404s /api/tenants/current and the whole dashboard.
        if (_environment.IsDevelopment() && HasDevTenantOverride(context))
        {
            await tenantContextService
                .ApplyFromRequestAsync(context, context.RequestAborted)
                .ConfigureAwait(false);
            if (tenantAccessor.TenantId.HasValue)
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            logger.LogDebug(
                "Development tenant override did not bind; falling through to JWT tenant_id");
        }

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            if (await RejectJwtHostMismatchAsync(
                    context,
                    tenantContextService,
                    authOptions.Value,
                    logger,
                    context.RequestAborted).ConfigureAwait(false))
            {
                return;
            }

            await tenantContextService
                .ApplyAuthenticatedTenantAsync(context, context.RequestAborted)
                .ConfigureAwait(false);
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// True when Development mandant switching should win over JWT.
    /// Platform slug <c>admin</c> is not a mandant override (FA localhost / admin host).
    /// Callers must also gate on Development — this helper does not check environment.
    /// </summary>
    public static bool HasDevTenantOverride(HttpContext context)
    {
        if (!TryGetRawDevOverrideSlug(context, out var rawSlug))
        {
            return false;
        }

        return !IsPlatformAdminSlug(rawSlug);
    }

    /// <summary>
    /// Shared platform hosts where JWT is the sole tenant authority (no Host slug match).
    /// </summary>
    public static bool IsSharedHost(HostString host) =>
        TenantHostNames.IsSharedPlatformHostForJwtMatch(host.Host);

    /// <returns><see langword="true"/> when the response was written and the pipeline must stop.</returns>
    private async Task<bool> RejectJwtHostMismatchAsync(
        HttpContext context,
        ITenantContextService tenantContextService,
        AuthOptions authOptions,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Development never enforces Host↔JWT match (DX + local switcher).
        if (_environment.IsDevelopment() || !authOptions.RequireTenantHostMatch)
            return false;

        if (IsSharedHost(context.Request.Host))
            return false;

        if (IsSuperAdminImpersonation(context.User))
        {
            logger.LogDebug("Super Admin impersonation — skipping tenant host match check");
            return false;
        }

        var jwtTenantId = GetJwtTenantId(context.User);
        if (!jwtTenantId.HasValue)
            return false;

        var hostTenantId = await tenantContextService
            .TryResolveHostBoundTenantIdAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (!hostTenantId.HasValue)
            return false;

        if (jwtTenantId.Value == hostTenantId.Value)
            return false;

        logger.LogWarning(
            "Tenant mismatch: JWT={JwtTenant}, Host={HostTenant}, HostName={HostName}, Path={Path}",
            jwtTenantId.Value,
            hostTenantId.Value,
            context.Request.Host.Host,
            context.Request.Path.Value);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        var error = new
        {
            error = "Forbidden",
            message = "Tenant mismatch between authentication and host",
            code = "TENANT_HOST_MISMATCH",
            status = 403,
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(error), cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsSuperAdminImpersonation(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        var raw = user.FindFirst(ImpersonationAuditContext.ImpersonationClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(raw)
            || !(string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
                 || raw == "1"))
        {
            return false;
        }

        if (user.IsInRole(Roles.SuperAdmin))
            return true;

        foreach (var claim in user.FindAll("role"))
        {
            if (string.Equals(claim.Value, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static Guid? GetJwtTenantId(ClaimsPrincipal? user)
    {
        var raw = user?.FindFirst(ScopeCheckService.TenantIdClaim)?.Value;
        return Guid.TryParse(raw, out var tenantId) && tenantId != Guid.Empty ? tenantId : null;
    }

    private static bool TryGetRawDevOverrideSlug(HttpContext context, out string slug)
    {
        if (context.Request.Headers.TryGetValue(SubdomainTenantProvider.DevTenantHeaderName, out var headerTenant)
            && !string.IsNullOrWhiteSpace(headerTenant))
        {
            slug = headerTenant.ToString().Trim();
            return true;
        }

        if (context.Request.Query.TryGetValue(SubdomainTenantProvider.DevTenantQueryName, out var queryTenant)
            && !string.IsNullOrWhiteSpace(queryTenant))
        {
            slug = queryTenant.ToString().Trim();
            return true;
        }

        slug = string.Empty;
        return false;
    }

    private static bool IsPlatformAdminSlug(string slug) =>
        string.Equals(slug, "admin", StringComparison.OrdinalIgnoreCase);
}
