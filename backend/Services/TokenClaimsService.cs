using System.Security.Claims;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Builds JWT/cookie claims: user id (<c>userId</c>, NameIdentifier, <c>user_id</c>, maps to <c>sub</c> in JWT), name, role (canonical), permission claims, optional tenant_id/branch_id.
/// Deterministic model: system roles => RolePermissionMatrix; custom roles => AspNetRoleClaims (via IRolePermissionResolver).
/// Each assigned role is emitted as a <c>role</c> claim so <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute"/> role checks see every role (not only the primary).
/// <para>
/// SuperAdmin JWTs emit only <see cref="AppPermissions.SystemCritical"/> (not the full catalog) so browser cookies stay under ~4KB;
/// <see cref="PermissionImplication"/> and role-matrix fallback treat that claim as full access. Login/me still return the full permission list from the resolver for UI.
/// </para>
/// </summary>
public sealed class TokenClaimsService : ITokenClaimsService
{
    private readonly IEffectivePermissionResolver _effectivePermissionResolver;

    public TokenClaimsService(IEffectivePermissionResolver effectivePermissionResolver)
    {
        _effectivePermissionResolver = effectivePermissionResolver;
    }

    /// <summary>Collects distinct canonical role names from Identity roles, or the user row when Identity has none.</summary>
    public static IReadOnlyList<string> CollectCanonicalRoles(IList<string>? identityRoles, string? userRoleColumn)
    {
        var canonical = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasIdentityRoles = false;
        if (identityRoles != null)
        {
            foreach (var r in identityRoles)
            {
                var c = RoleCanonicalization.GetCanonicalRole(r);
                if (string.IsNullOrEmpty(c))
                    continue;
                hasIdentityRoles = true;
                canonical.Add(c);
            }
        }

        if (!hasIdentityRoles)
        {
            var fromUser = RoleCanonicalization.GetCanonicalRole(userRoleColumn);
            if (!string.IsNullOrEmpty(fromUser))
                canonical.Add(fromUser);
        }

        if (canonical.Count == 0)
            canonical.Add(Roles.FallbackUnknown);

        return canonical.OrderBy(r => r, StringComparer.Ordinal).ToList();
    }

    /// <summary>Picks the display/primary role when multiple are assigned (highest precedence in <see cref="Roles.Canonical"/>).</summary>
    public static string ResolvePrimaryRole(IReadOnlyCollection<string> canonicalRoles)
    {
        if (canonicalRoles.Count == 0)
            return Roles.FallbackUnknown;

        foreach (var preferred in Roles.Canonical)
        {
            if (canonicalRoles.Contains(preferred, StringComparer.OrdinalIgnoreCase))
                return preferred;
        }

        return canonicalRoles.First();
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
        list.Add(new Claim("userId", user.Id));
        list.Add(new Claim(ClaimTypes.Email, user.Email ?? string.Empty));
        list.Add(new Claim(ClaimTypes.Name, user.Name));
        list.Add(new Claim("user_id", user.Id));

        var canonicalRoles = CollectCanonicalRoles(roles, user.Role);

        // JwtBearer RoleClaimType is "role"; [Authorize(Roles=...)] requires one claim per role.
        foreach (var role in canonicalRoles)
            list.Add(new Claim("role", role));

        foreach (var role in canonicalRoles)
            list.Add(new Claim("roles", role));

        var isSuperAdmin = canonicalRoles.Any(r =>
            string.Equals(r, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase));

        if (isSuperAdmin)
        {
            // Compact token: full catalog (~137 claims) exceeds typical browser cookie limits (~4KB).
            list.Add(new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.SystemCritical));
        }
        else
        {
            var roleNamesForResolver = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasIdentityRoles = false;
            if (roles != null)
            {
                foreach (var r in roles)
                {
                    if (string.IsNullOrWhiteSpace(r))
                        continue;
                    hasIdentityRoles = true;
                    roleNamesForResolver.Add(r.Trim());
                }
            }

            if (!hasIdentityRoles && !string.IsNullOrWhiteSpace(user.Role))
                roleNamesForResolver.Add(user.Role.Trim());

            Guid? tenantGuid = Guid.TryParse(tenantId, out var parsedTenantId) ? parsedTenantId : null;
            var effectivePermissions = await _effectivePermissionResolver.GetEffectivePermissionsAsync(
                user.Id,
                roleNamesForResolver,
                tenantGuid,
                cancellationToken);
            // Admin login/me: strip POS write ops; Manager keeps oversight reads (payment.view, sale.view, report.*).
            var permissions = AdminAppPermissionProfile.Filter(appContext, canonicalRoles, effectivePermissions);
            foreach (var p in permissions)
                list.Add(new Claim(PermissionCatalog.PermissionClaimType, p));
        }

        if (!string.IsNullOrEmpty(tenantId))
            list.Add(new Claim(ScopeCheckService.TenantIdClaim, tenantId));
        if (!string.IsNullOrEmpty(branchId))
            list.Add(new Claim(ScopeCheckService.BranchIdClaim, branchId));

        if (!string.IsNullOrEmpty(appContext))
            list.Add(new Claim(Authorization.ClientAppPolicy.AppContextClaimType, appContext));

        return list;
    }
}
