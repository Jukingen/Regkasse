using System.Security.Claims;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Builds JWT/cookie claims: sub, name, role (canonical), permission claims, optional tenant_id/branch_id.
/// Deterministic model: system roles => RolePermissionMatrix; custom roles => AspNetRoleClaims (via IRolePermissionResolver).
/// </summary>
public sealed class TokenClaimsService : ITokenClaimsService
{
    private readonly IRolePermissionResolver _rolePermissionResolver;

    public TokenClaimsService(IRolePermissionResolver rolePermissionResolver)
    {
        _rolePermissionResolver = rolePermissionResolver;
    }

    public async Task<IReadOnlyList<Claim>> BuildClaimsAsync(
        ApplicationUser user,
        IList<string> roles,
        string? tenantId = null,
        string? branchId = null,
        string? appContext = null,
        CancellationToken cancellationToken = default)
    {
        var list = new List<Claim>();

        list.Add(new Claim(ClaimTypes.NameIdentifier, user.Id));
        list.Add(new Claim(ClaimTypes.Email, user.Email ?? string.Empty));
        list.Add(new Claim(ClaimTypes.Name, user.Name));
        list.Add(new Claim("user_id", user.Id));

        var primaryRole = roles?.FirstOrDefault() ?? user.Role ?? Roles.FallbackUnknown;
        var canonicalRole = RoleCanonicalization.GetCanonicalRole(primaryRole);
        list.Add(new Claim("role", canonicalRole));
        list.Add(new Claim(ClaimTypes.Role, canonicalRole));

        if (roles != null && roles.Count > 0)
        {
            foreach (var r in roles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var canonical = RoleCanonicalization.GetCanonicalRole(r);
                if (!string.IsNullOrEmpty(canonical))
                    list.Add(new Claim("roles", canonical));
            }
        }

        var permissions = await _rolePermissionResolver.GetPermissionsForRolesAsync(roles ?? Array.Empty<string>(), cancellationToken);
        foreach (var p in permissions)
            list.Add(new Claim(PermissionCatalog.PermissionClaimType, p));

        if (!string.IsNullOrEmpty(tenantId))
            list.Add(new Claim(ScopeCheckService.TenantIdClaim, tenantId));
        if (!string.IsNullOrEmpty(branchId))
            list.Add(new Claim(ScopeCheckService.BranchIdClaim, branchId));

        if (!string.IsNullOrEmpty(appContext))
            list.Add(new Claim(Authorization.ClientAppPolicy.AppContextClaimType, appContext));

        return list;
    }
}
