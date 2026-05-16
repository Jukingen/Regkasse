using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Sets <see cref="ICurrentTenantAccessor"/> from the authenticated user's <c>tenant_id</c> JWT claim.
/// </summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICurrentTenantAccessor tenantAccessor)
    {
        var raw = context.User?.FindFirst(ScopeCheckService.TenantIdClaim)?.Value;
        if (Guid.TryParse(raw, out var tenantId) && tenantId != Guid.Empty)
            tenantAccessor.TenantId = tenantId;

        await _next(context);
    }
}
