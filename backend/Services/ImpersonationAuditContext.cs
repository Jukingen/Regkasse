using System.Security.Claims;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Services;

/// <summary>
/// Resolves impersonation actor and target tenant for immutable audit rows.
/// </summary>
public static class ImpersonationAuditContext
{
    public const string ImpersonationClaimType = "tenant_impersonation";

    public readonly record struct Snapshot(string? ImpersonatedBy, Guid? ImpersonatedTenantId)
    {
        public bool IsActive => !string.IsNullOrWhiteSpace(ImpersonatedBy);
    }

    public static Snapshot FromHttpContext(HttpContext? httpContext, ICurrentTenantAccessor? tenantAccessor = null)
    {
        if (httpContext?.User == null)
        {
            return default;
        }

        return FromClaimsPrincipal(httpContext.User, tenantAccessor);
    }

    public static Snapshot FromClaimsPrincipal(ClaimsPrincipal? user, ICurrentTenantAccessor? tenantAccessor = null)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return default;
        }

        if (!IsTruthyClaim(user.FindFirst(ImpersonationClaimType)?.Value))
        {
            return default;
        }

        var actorId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("user_id")?.Value
            ?? user.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return default;
        }

        var tenantId = ResolveTenantId(user, tenantAccessor);
        return new Snapshot(actorId.Trim(), tenantId);
    }

    /// <summary>Explicit snapshot when impersonation session is issued (admin host, before tenant JWT is active).</summary>
    public static Snapshot ForSessionStart(string superAdminUserId, Guid impersonatedTenantId)
    {
        if (string.IsNullOrWhiteSpace(superAdminUserId))
        {
            return default;
        }

        return new Snapshot(superAdminUserId.Trim(), impersonatedTenantId);
    }

    public static void ApplyTo(AuditLog auditLog, Snapshot snapshot)
    {
        if (!snapshot.IsActive)
        {
            return;
        }

        auditLog.ImpersonatedBy = snapshot.ImpersonatedBy!.Length > 450
            ? snapshot.ImpersonatedBy[..450]
            : snapshot.ImpersonatedBy;
        auditLog.ImpersonatedTenantId = snapshot.ImpersonatedTenantId;
    }

    private static Guid? ResolveTenantId(ClaimsPrincipal user, ICurrentTenantAccessor? tenantAccessor)
    {
        var tenantIdRaw = user.FindFirst(ScopeCheckService.TenantIdClaim)?.Value;
        if (Guid.TryParse(tenantIdRaw, out var fromClaim))
        {
            return fromClaim;
        }

        return tenantAccessor?.TenantId;
    }

    private static bool IsTruthyClaim(string? value) =>
        value is "true" or "True" or "1" or "yes" or "Yes";
}
