using KasseAPI_Final.Logging;
using KasseAPI_Final.Security;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// After authentication + tenant binding, pushes Tenant/User/Role/CorrelationId into the logging scope
/// so subsequent logs carry unmasked debugging context without repeating it in every message.
/// </summary>
public sealed class RequestLoggingScopeMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLoggingScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<RequestLoggingScopeMiddleware> logger)
    {
        var scope = BuildScope(context, tenantAccessor);
        if (scope.Count == 0)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        using (logger.BeginScope(scope))
        {
            await _next(context).ConfigureAwait(false);
        }
    }

    internal static Dictionary<string, object> BuildScope(
        HttpContext context,
        ICurrentTenantAccessor tenantAccessor)
    {
        var scope = new Dictionary<string, object>(StringComparer.Ordinal);

        var tenantSlug = tenantAccessor.TenantSlug;
        var tenantId = tenantAccessor.TenantId;
        if (!string.IsNullOrWhiteSpace(tenantSlug) || tenantId.HasValue)
        {
            scope[LogContextKeys.Tenant] = string.IsNullOrWhiteSpace(tenantSlug) ? "-" : tenantSlug.Trim();
            if (tenantId.HasValue)
                scope[LogContextKeys.TenantId] = LogIdFormatting.ShortGuid(tenantId.Value);
        }

        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var email = user.GetActorEmail();
            var userId = user.GetActorUserId();
            var role = user.GetActorRole();

            if (!string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(userId))
            {
                scope[LogContextKeys.User] = string.IsNullOrWhiteSpace(email) ? "-" : email.Trim();
                if (!string.IsNullOrWhiteSpace(userId))
                    scope[LogContextKeys.UserId] = LogIdFormatting.ShortId(userId);
            }

            if (!string.IsNullOrWhiteSpace(role))
                scope[LogContextKeys.Role] = role.Trim();
        }

        if (context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var correlationObj)
            && correlationObj is string correlationId
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            scope[LogContextKeys.CorrelationId] = LogIdFormatting.ShortId(correlationId);
        }

        return scope;
    }
}
