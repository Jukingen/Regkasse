using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// FA (frontend-admin) mandant license lockdown / permission restriction.
/// When the tenant is <see cref="LicenseLifecycleState.Locked"/> or
/// <see cref="LicenseLifecycleState.Archived"/>:
/// <list type="bullet">
/// <item>Allow all safe reads (GET/HEAD/OPTIONS) — FA stays usable for renewal &amp; GDPR.</item>
/// <item>Block write operations except license renewal / activation and data-management.</item>
/// </list>
/// POS lockdown remains in <see cref="LicenseMiddleware"/>.
/// Broader tenant soft-delete / maintenance gates: <see cref="TenantOperationalGateMiddleware"/>.
/// </summary>
public sealed class LicenseLockdownMiddleware
{
    public const string LicenseExpiredCode = "LICENSE_EXPIRED";

    private readonly RequestDelegate _next;
    private readonly ILogger<LicenseLockdownMiddleware> _logger;

    public LicenseLockdownMiddleware(RequestDelegate next, ILogger<LicenseLockdownMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ILicenseService licenseService,
        ICurrentTenantAccessor tenantAccessor,
        IHostEnvironment environment,
        IOptions<TseOptions> tseOptions,
        IOptions<LicenseOptions> licenseOptions,
        IDevelopmentModeService developmentMode)
    {
        // Only apply to authenticated FA traffic.
        if (context.User?.Identity?.IsAuthenticated != true
            || !IsFaRequest(context)
            || ShouldSkip(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (LicenseEnforcementPolicy.ShouldDisableEnforcement(
                environment,
                tseOptions.Value,
                developmentMode,
                licenseOptions.Value))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var tenantId = tenantAccessor.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Super Admin may renew / unlock / support regardless of mandant license state.
        if (context.User.IsInRole(Roles.SuperAdmin))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var licenseStatus = await licenseService
            .GetLicenseStatusAsync(tenantId.Value, context.RequestAborted)
            .ConfigureAwait(false);

        var state = ResolveLifecycleState(licenseStatus);

        // Active / Grace → full FA permissions.
        if (state is LicenseLifecycleState.Active or LicenseLifecycleState.Grace)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Locked / Archived (and ExportRequest / Deleted via overdue mapping) → restrict.
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        if (IsReadMethod(method))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (IsAllowedWriteOperation(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        _logger.LogWarning(
            "FA request blocked by mandant license lockdown. TenantId={TenantId}, State={State}, Path={Path}, Method={Method}",
            tenantId,
            state,
            path,
            method);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            new
            {
                Error = LicenseExpiredCode,
                Message = "License has expired. Write operations are disabled.",
                State = state.ToString(),
                ExpiredAt = licenseStatus.ValidUntil,
                DaysOverdue = licenseStatus.DaysOverdue,
                GraceEnded = licenseStatus.LockDate,
            },
            context.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>
    /// FA traffic: JWT <c>app_context=admin</c>, or <c>/api/admin/*</c> when not a POS operation.
    /// </summary>
    internal static bool IsFaRequest(HttpContext context)
    {
        if (LicenseMiddleware.IsPosOperation(context))
            return false;

        var appContext = LicensePathFeatureEvaluator.ReadAppContext(context);
        if (string.Equals(appContext, ClientAppPolicy.Admin, StringComparison.OrdinalIgnoreCase))
            return true;

        var path = context.Request.Path.Value ?? string.Empty;
        return path.StartsWith("/api/admin/", StringComparison.OrdinalIgnoreCase);
    }

    internal static LicenseLifecycleState ResolveLifecycleState(LicenseStatusInfo licenseStatus)
    {
        if (!licenseStatus.IsExpired)
            return LicenseLifecycleState.Active;

        if (licenseStatus.IsInGracePeriod)
            return LicenseLifecycleState.Grace;

        if (licenseStatus.DaysOverdue > LicenseGracePeriodConfig.ArchiveAfterDays)
            return LicenseLifecycleState.Archived;

        return LicenseLifecycleState.Locked;
    }

    /// <summary>
    /// Mutations still allowed under Locked/Archived: license renewal/activation and GDPR data-management.
    /// (Does not block GETs — FA remains read-only for oversight.)
    /// </summary>
    internal static bool IsAllowedWriteOperation(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // License renewal / extend / activate (FA + billing).
        if (path.StartsWith("/api/admin/license", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.StartsWith("/api/admin/licenses", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.StartsWith("/api/admin/billing/license-sales", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.StartsWith("/api/license", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.Contains("/license", StringComparison.OrdinalIgnoreCase)
            && path.StartsWith("/api/admin/tenants/", StringComparison.OrdinalIgnoreCase))
            return true;

        // GDPR export / account closure under expired license.
        if (path.Contains("/data-management", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>Backward-compatible name used by older tests/callers.</summary>
    internal static bool IsAllowedPath(string path, string method) =>
        IsReadMethod(method) || IsAllowedWriteOperation(path);

    private static bool IsReadMethod(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method);

    private static bool ShouldSkip(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/csrf/", StringComparison.OrdinalIgnoreCase);
    }
}
