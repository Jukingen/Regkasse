using System.Net.Mime;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Maintenance;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Platform maintenance limited mode: allows safe reads + critical auth/status paths;
/// blocks other writes with HTTP 503. SuperAdmin always passes (with <c>X-Maintenance-Mode: true</c>).
/// Runs after authentication.
/// </summary>
public sealed class MaintenanceMiddleware
{
    public const string MaintenanceModeHeaderName = "X-Maintenance-Mode";
    public const string SystemInMaintenanceCode = "SystemInMaintenance";
    public const string LimitedModeValue = "limited";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly MaintenanceOperationFilter _operationFilter;

    public MaintenanceMiddleware(RequestDelegate next, MaintenanceOperationFilter operationFilter)
    {
        _next = next;
        _operationFilter = operationFilter;
    }

    public async Task InvokeAsync(HttpContext context, IMaintenanceModeService maintenanceService)
    {
        var status = await maintenanceService
            .GetCurrentStatusAsync(context.RequestAborted)
            .ConfigureAwait(false);

        if (!status.IsActive)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Advertise limited mode on all responses while the window is active.
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(MaintenanceModeHeaderName))
                context.Response.Headers.Append(MaintenanceModeHeaderName, LimitedModeValue);
            return Task.CompletedTask;
        });

        // SuperAdmin always has full access.
        if (IsSuperAdmin(context))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        if (_operationFilter.IsOperationAllowed(method, path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = MediaTypeNames.Application.Json;
        context.Response.Headers[MaintenanceModeHeaderName] = LimitedModeValue;
        if (status.ScheduledEndAt is DateTime end)
        {
            var retrySeconds = Math.Max(30, (int)Math.Ceiling((end - DateTime.UtcNow).TotalSeconds));
            context.Response.Headers.Append("Retry-After", retrySeconds.ToString());
        }

        var payload = new
        {
            error = SystemInMaintenanceCode,
            code = SystemInMaintenanceCode,
            mode = LimitedModeValue,
            message = status.Message
                ?? "System is currently undergoing maintenance. Read access is available; write operations are temporarily disabled.",
            scheduledEnd = status.ScheduledEndAt,
            estimatedRemaining = status.ScheduledEndAt.HasValue
                ? status.ScheduledEndAt.Value - DateTime.UtcNow
                : (TimeSpan?)null,
            title = status.Title,
            highRisk = _operationFilter.IsHighRiskBlockedWrite(method, path),
        };

        await context.Response
            .WriteAsync(JsonSerializer.Serialize(payload, JsonOptions), context.RequestAborted)
            .ConfigureAwait(false);
    }

    private static bool IsSuperAdmin(HttpContext context) =>
        context.User?.IsInRole(Roles.SuperAdmin) == true;

    /// <summary>
    /// Paths that must remain reachable so clients can learn status, authenticate, and acknowledge notices.
    /// </summary>
    internal static bool IsCriticalPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // Health / probes
        if (StartsWithOrdinalIgnoreCase(path, "/api/health")
            || StartsWithOrdinalIgnoreCase(path, "/health")
            || StartsWithOrdinalIgnoreCase(path, "/metrics"))
            return true;

        // Auth (login + refresh so users can see maintenance banners after re-auth)
        if (StartsWithOrdinalIgnoreCase(path, "/api/Auth")
            || StartsWithOrdinalIgnoreCase(path, "/api/auth"))
            return true;

        // CSRF token bootstrap
        if (StartsWithOrdinalIgnoreCase(path, "/api/csrf"))
            return true;

        // Shared / surface-specific maintenance status + notification read/ack
        if (StartsWithOrdinalIgnoreCase(path, "/api/maintenance")
            || StartsWithOrdinalIgnoreCase(path, "/api/admin/maintenance")
            || StartsWithOrdinalIgnoreCase(path, "/api/pos/maintenance"))
            return true;
        if (StartsWithOrdinalIgnoreCase(path, "/api/admin/maintenance-notifications/active")
            || ContainsAcknowledge(path, "/api/admin/maintenance-notifications/"))
            return true;
        if (StartsWithOrdinalIgnoreCase(path, "/api/pos/maintenance-notifications/active")
            || ContainsAcknowledge(path, "/api/pos/maintenance-notifications/"))
            return true;

        // Swagger in any env where it is mapped
        if (StartsWithOrdinalIgnoreCase(path, "/swagger"))
            return true;

        return false;
    }

    private static bool ContainsAcknowledge(string path, string prefix) =>
        StartsWithOrdinalIgnoreCase(path, prefix)
        && path.Contains("/acknowledge", StringComparison.OrdinalIgnoreCase);

    private static bool StartsWithOrdinalIgnoreCase(string path, string prefix) =>
        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
