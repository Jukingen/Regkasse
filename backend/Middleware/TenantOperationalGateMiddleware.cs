using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Localization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Localization;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Blocks tenant-scoped API traffic when the ambient tenant is soft-deleted, suspended,
/// in operation mode (maintenance / read-only), or restricted by tenant-license policy.
/// </summary>
public sealed class TenantOperationalGateMiddleware
{
    public const string TenantLicenseStatusHeaderName = "X-Tenant-License-Status";
    public const string TenantLicenseWarningHeaderName = "X-Tenant-License-Warning";
    public const string LicenseDaysExpiredHeaderName = "X-License-Days-Expired";
    public const string TenantOperationModeHeaderName = "X-Tenant-Operation-Mode";
    public const string TenantMaintenanceMessageHeaderName = "X-Tenant-Maintenance-Message";

    private readonly RequestDelegate _next;

    public TenantOperationalGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenantAccessor tenantAccessor,
        AppDbContext db,
        ILogger<TenantOperationalGateMiddleware> logger,
        TenantLicenseValidator tenantLicenseValidator,
        IHostEnvironment environment,
        IOptions<TseOptions> tseOptions,
        IOptions<LicenseOptions> licenseOptions,
        IDevelopmentModeService developmentMode,
        IApiMessageLocalizer messages)
    {
        if (ShouldSkip(context.Request.Path) || tenantAccessor.TenantId is not Guid tenantId)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var isSuperAdmin = context.User.IsInRole("SuperAdmin");
        var path = context.Request.Path;
        var method = context.Request.Method;
        var isWriteOperation = IsWriteMethod(method);
        var isReadOnlyOperation = HttpMethods.IsGet(method);
        var isReportPath = IsReportPath(path);
        var isUserManagementPath = IsUserManagementPath(path);

        var row = await db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => new
            {
                t.Status,
                t.IsActive,
                t.Name,
                t.LicenseValidUntilUtc,
                t.OperationMode,
                t.MaintenanceMessage,
                t.MaintenanceStartedAt,
                t.MaintenanceEndsAt,
            })
            .FirstOrDefaultAsync(context.RequestAborted)
            .ConfigureAwait(false);

        if (row == null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (string.Equals(row.Status, TenantStatuses.Deleted, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = messages.Get(ApiMessageKeys.TenantDisabled),
                code = LoginTenantBlockedException.CodeTenantDisabled,
                tenantName = row.Name,
            }).ConfigureAwait(false);
            return;
        }

        if (!row.IsActive || string.Equals(row.Status, TenantStatuses.Suspended, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Dieser Mandant ist derzeit nicht aktiv.",
                code = "TENANT_NOT_ACTIVE",
                tenantName = row.Name,
            }).ConfigureAwait(false);
            return;
        }

        var utcNow = DateTime.UtcNow;
        var effectiveMode = TenantOperationModes.Normalize(row.OperationMode);
        if (effectiveMode == TenantOperationModes.Maintenance
            && !TenantOperationModes.IsMaintenanceActive(row.OperationMode, row.MaintenanceEndsAt, utcNow))
        {
            // Window expired — treat as active for gating until Super Admin clears the flag.
            effectiveMode = TenantOperationModes.Active;
        }

        context.Response.Headers[TenantOperationModeHeaderName] = effectiveMode;
        if (!string.IsNullOrWhiteSpace(row.MaintenanceMessage))
            context.Response.Headers[TenantMaintenanceMessageHeaderName] = row.MaintenanceMessage;

        // Super Admin bypasses operation-mode gates (still subject to lifecycle above).
        // Maintenance: 503 Service Unavailable for all methods (POS/FA unavailable).
        // Read-only: 403 Forbidden for write methods only.
        if (!isSuperAdmin
            && !IsOperationModeManagementPath(path)
            && effectiveMode == TenantOperationModes.Maintenance)
        {
            var message = string.IsNullOrWhiteSpace(row.MaintenanceMessage)
                ? "This tenant is currently in maintenance mode"
                : row.MaintenanceMessage;

            logger.LogWarning(
                "Tenant request blocked by maintenance mode. TenantId={TenantId}, Path={Path}, Method={Method}",
                tenantId,
                path.Value,
                method);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "TenantInMaintenance",
                code = "TenantInMaintenance",
                message,
                estimatedEnd = row.MaintenanceEndsAt,
                operationMode = effectiveMode,
            }).ConfigureAwait(false);
            return;
        }

        if (!isSuperAdmin
            && isWriteOperation
            && !IsOperationModeManagementPath(path)
            && !IsDataManagementPath(path)
            && effectiveMode == TenantOperationModes.Readonly)
        {
            logger.LogWarning(
                "Tenant write blocked by read-only mode. TenantId={TenantId}, Path={Path}, Method={Method}",
                tenantId,
                path.Value,
                method);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "TenantReadOnly",
                code = "TenantReadOnly",
                message = "This tenant is in read-only mode",
                reason = "Please contact support for assistance",
                operationMode = effectiveMode,
            }).ConfigureAwait(false);
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

        var licenseStatus = tenantLicenseValidator.GetStatus(row.LicenseValidUntilUtc);
        ApplyLicenseHeaders(context, licenseStatus, row.LicenseValidUntilUtc);

        var permissions = tenantLicenseValidator.GetPermissions(row.LicenseValidUntilUtc, isSuperAdmin);
        if (isUserManagementPath && isWriteOperation && !permissions.CanManageUsers)
        {
            logger.LogWarning(
                "Tenant user management write blocked by expired license. TenantId={TenantId}, Path={Path}, Method={Method}",
                tenantId,
                path.Value,
                method);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "LICENSE_EXPIRED_USER_MGMT_BLOCKED",
                message = "Die Benutzerverwaltung ist aufgrund abgelaufener Lizenz deaktiviert. Bitte verlaengern Sie die Lizenz.",
            }).ConfigureAwait(false);
            return;
        }

        if (!permissions.CanWrite
            && isWriteOperation
            && !isReportPath
            && !IsDataManagementPath(path)
            && !(isUserManagementPath && permissions.CanManageUsers))
        {
            logger.LogWarning(
                "Tenant write blocked by expired license. TenantId={TenantId}, Status={Status}, Path={Path}, Method={Method}",
                tenantId,
                licenseStatus,
                path.Value,
                method);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "LICENSE_EXPIRED_WRITE_BLOCKED",
                message = "Die Lizenz ist abgelaufen. Schreiboperationen sind deaktiviert. Sie koennen weiterhin Berichte einsehen und Daten exportieren.",
                canManageUsers = permissions.CanManageUsers,
            }).ConfigureAwait(false);
            return;
        }

        if (!permissions.CanAccess && !isReadOnlyOperation && !isReportPath && !IsDataManagementPath(path))
        {
            logger.LogWarning(
                "Tenant non-read request blocked by tenant license lockdown. TenantId={TenantId}, Path={Path}, Method={Method}",
                tenantId,
                path.Value,
                method);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "LICENSE_EXPIRED_WRITE_BLOCKED",
                message = "Die Lizenz ist abgelaufen. Schreiboperationen sind deaktiviert. Sie koennen weiterhin Berichte einsehen und Daten exportieren.",
                canManageUsers = permissions.CanManageUsers,
            }).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool ShouldSkip(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWriteMethod(string method) =>
        HttpMethods.IsPost(method)
        || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method)
        || HttpMethods.IsDelete(method);

    private static bool IsReportPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.Contains("/api/reports", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/api/receipts", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Expired-license data export / deletion-request endpoints remain writable.</summary>
    private static bool IsDataManagementPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.Contains("/data-management", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Allow Super Admin to change operation mode even when the tenant is readonly/maintenance.</summary>
    private static bool IsOperationModeManagementPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.Contains("/operation-mode", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUserManagementPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.StartsWith("/api/admin/users", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/usermanagement", StringComparison.OrdinalIgnoreCase)
            || (value.StartsWith("/api/admin/tenants/", StringComparison.OrdinalIgnoreCase)
                && value.Contains("/users", StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyLicenseHeaders(
        HttpContext context,
        TenantLicenseStatus status,
        DateTime? validUntilUtc)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Headers[TenantLicenseStatusHeaderName] = status.ToString();
        if (status != TenantLicenseStatus.GraceWrite)
            return;

        context.Response.Headers[TenantLicenseWarningHeaderName] = "expired_grace";
        if (validUntilUtc.HasValue)
        {
            var daysExpired = Math.Max(0, (DateTime.UtcNow - DateTime.SpecifyKind(validUntilUtc.Value, DateTimeKind.Utc)).Days);
            context.Response.Headers[LicenseDaysExpiredHeaderName] = daysExpired.ToString();
        }
    }
}
