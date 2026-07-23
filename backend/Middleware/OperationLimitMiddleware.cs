using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Operations;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Enforces per-tenant operation quotas (bulk delete, product create/update, users, backup, export).
/// Gated by <see cref="TenantOperationLimitsOptions.Enabled"/> (default off).
/// </summary>
public sealed class OperationLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OperationLimitMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<TenantOperationLimitsOptions> _options;

    public OperationLimitMiddleware(
        RequestDelegate next,
        ILogger<OperationLimitMiddleware> logger,
        IHostEnvironment environment,
        IOptionsMonitor<TenantOperationLimitsOptions> options)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _options = options;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IOperationLimitService limitService,
        ICurrentTenantAccessor tenantAccessor)
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
        var kind = limitService.MatchOperation(method, path);
        if (kind is null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var tenantId = tenantAccessor.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            // Super Admin / no ambient tenant: do not invent a quota bucket.
            await _next(context).ConfigureAwait(false);
            return;
        }

        var userId = context.User.GetActorUserId()
                     ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        var quantity = await ResolveQuantityAsync(context.Request, kind.Value).ConfigureAwait(false);
        var hasApproval = !string.IsNullOrWhiteSpace(
            context.Request.Headers[CriticalActionOptions.ApprovalHeaderName].FirstOrDefault());

        var check = await limitService
            .CheckLimitAsync(tenantId.Value, userId, kind.Value, quantity, hasApproval, context.RequestAborted)
            .ConfigureAwait(false);

        if (!check.IsAllowed)
        {
            var status = check.RequiresApproval
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status429TooManyRequests;

            _logger.LogWarning(
                "Operation limit blocked tenant {TenantId} user {UserId} kind {Kind} code {Code} qty {Qty}",
                tenantId,
                userId,
                kind,
                check.Code,
                quantity);

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new
            {
                code = check.Code,
                error = check.RequiresApproval ? "OperationRequiresApproval" : "OperationLimitExceeded",
                message = check.Message,
                limit = check.Limit,
                current = check.Current,
                remaining = check.Remaining,
                resetAt = check.ResetAt,
                kind = kind.ToString(),
                requiresApproval = check.RequiresApproval,
            }).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);

        // Only consume quota when the downstream handler succeeded.
        if (context.Response.StatusCode is >= 200 and < 400)
        {
            await limitService
                .RecordOperationAsync(tenantId.Value, userId, kind.Value, quantity, context.RequestAborted)
                .ConfigureAwait(false);
        }
    }

    private static async Task<int> ResolveQuantityAsync(HttpRequest request, TenantOperationLimitKind kind)
    {
        if (kind != TenantOperationLimitKind.BulkDelete)
            return 1;

        var path = request.Path.Value ?? string.Empty;
        if (path.Contains("deactivate-all", StringComparison.OrdinalIgnoreCase))
        {
            // Unknown catalog size here; treat as one operation unit against the daily quota.
            // Controllers still enforce confirm phrases / critical-action gates.
            return 1;
        }

        try
        {
            request.EnableBuffering();
            if (request.Body.CanSeek)
                request.Body.Position = 0;

            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var json = await reader.ReadToEndAsync().ConfigureAwait(false);
            if (request.Body.CanSeek)
                request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(json))
                return 1;

            using var doc = JsonDocument.Parse(json);
            if (TryGetArrayLength(doc.RootElement, "productIds", out var count)
                || TryGetArrayLength(doc.RootElement, "ProductIds", out count))
            {
                return Math.Max(1, count);
            }
        }
        catch (JsonException)
        {
            // Fall through — controller will validate body.
        }

        return 1;
    }

    private static bool TryGetArrayLength(JsonElement root, string name, out int length)
    {
        length = 0;
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return false;
        length = prop.GetArrayLength();
        return true;
    }
}
