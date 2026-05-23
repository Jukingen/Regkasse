using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Middleware;

/// <summary>
/// Blocks tenant-scoped API traffic when the ambient tenant is soft-deleted or not operational.
/// Super-admin routes under <c>/api/admin/</c> are exempt.
/// </summary>
public sealed class TenantOperationalGateMiddleware
{
    private readonly RequestDelegate _next;

    public TenantOperationalGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenantAccessor tenantAccessor, AppDbContext db)
    {
        if (ShouldSkip(context.Request.Path) || tenantAccessor.TenantId is not Guid tenantId)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var row = await db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Status, t.IsActive, t.Name })
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
                message = Services.Auth.AuthService.TenantDisabledMessageDe,
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

        await _next(context).ConfigureAwait(false);
    }

    private static bool ShouldSkip(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.StartsWith("/api/admin/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
    }
}
