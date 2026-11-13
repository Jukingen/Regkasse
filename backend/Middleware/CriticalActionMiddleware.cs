using System.Security.Claims;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.CriticalActions;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Enforces two-step confirmation for configured critical admin paths.
/// Clients must send <c>X-Critical-Action-Approval</c> with a token issued after 2FA
/// or Super Admin approval (<see cref="ICriticalActionApprovalService"/>).
/// Gated by <see cref="CriticalActionOptions.Enabled"/> (default off).
/// </summary>
public sealed class CriticalActionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CriticalActionMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<CriticalActionOptions> _options;

    public CriticalActionMiddleware(
        RequestDelegate next,
        ILogger<CriticalActionMiddleware> logger,
        IHostEnvironment environment,
        IOptionsMonitor<CriticalActionOptions> options)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context, ICriticalActionApprovalService approvalService)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (_environment.IsDevelopment() && options.BypassInDevelopment)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;
        var actionType = approvalService.MatchCriticalAction(method, path);
        if (actionType is null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var approvalHeader = context.Request.Headers[CriticalActionOptions.ApprovalHeaderName]
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(approvalHeader))
        {
            _logger.LogWarning(
                "Critical action blocked (missing approval) for {Method} {Path} action {ActionType}",
                method,
                path,
                actionType);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "CRITICAL_ACTION_REQUIRES_APPROVAL",
                error = "CriticalActionRequiresApproval",
                message = "This action requires SuperAdmin approval or 2FA",
                actionType = actionType.ToString(),
                steps = new[] { "Confirm intention", "Get SuperAdmin approval or enter 2FA", "Execute action" },
            }).ConfigureAwait(false);
            return;
        }

        var userId = context.User.GetActorUserId()
                     ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "AUTH_REQUIRED",
                error = "Unauthorized",
                message = "Authentication required for critical actions",
            }).ConfigureAwait(false);
            return;
        }

        var isValid = await approvalService
            .VerifyApprovalAsync(userId, approvalHeader, path, context.RequestAborted)
            .ConfigureAwait(false);

        if (!isValid)
        {
            _logger.LogWarning(
                "Critical action blocked (invalid approval) for user {UserId} {Method} {Path}",
                userId,
                method,
                path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "INVALID_APPROVAL",
                error = "InvalidApproval",
                message = "The approval token is invalid or expired",
                actionType = actionType.ToString(),
            }).ConfigureAwait(false);
            return;
        }

        _logger.LogInformation(
            "Critical action approval accepted for user {UserId} {Method} {Path} action {ActionType}",
            userId,
            method,
            path,
            actionType);

        await _next(context).ConfigureAwait(false);
    }
}
