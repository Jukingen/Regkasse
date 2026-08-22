using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Builds compact JWT claims for cookie-safe tokens (~4KB browser limit).
/// Identity: <c>sub</c>, <c>userId</c>, <c>email</c>, one <c>role</c> per assigned role.
/// Permissions: SuperAdmin emits only <see cref="AppPermissions.SystemCritical"/>; other canonical
/// system roles omit the catalog (authorization uses role matrix + <see cref="AdminAppPermissionProfile"/>).
/// Custom roles still embed filtered <c>permission</c> claims. Login/me JSON still returns the full list.
/// </summary>
public sealed class TokenClaimsService : ITokenClaimsService
{
    /// <summary>Compact JWT claim for Identity <see cref="ApplicationUser.SecurityStamp"/> (force-logout invalidation).</summary>
    public const string SecurityStampClaimType = "sst";

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

    /// <summary>
    /// Custom (non-matrix) roles must keep permission claims. Canonical system roles do not —
    /// the catalog would overflow the FA proxy cookie.
    /// </summary>
    public static bool ShouldEmbedPermissionClaims(IReadOnlyList<string> canonicalRoles)
    {
        if (canonicalRoles.Count == 0)
            return false;

        foreach (var role in canonicalRoles)
        {
            if (!Roles.IsCanonical(role))
                return true;
        }

        return false;
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

        list.Add(new Claim(JwtRegisteredClaimNames.Sub, user.Id));
        list.Add(new Claim("userId", user.Id));
        if (!string.IsNullOrWhiteSpace(user.Email))
            list.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        if (!string.IsNullOrEmpty(user.SecurityStamp))
            list.Add(new Claim(SecurityStampClaimType, user.SecurityStamp));

        var canonicalRoles = CollectCanonicalRoles(roles, user.Role);

        // JwtBearer RoleClaimType is "role"; [Authorize(Roles=...)] requires one claim per role.
        foreach (var role in canonicalRoles)
            list.Add(new Claim("role", role));

        var isSuperAdmin = canonicalRoles.Any(r =>
            string.Equals(r, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase));

        if (isSuperAdmin)
        {
            list.Add(new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.SystemCritical));
        }
        else if (ShouldEmbedPermissionClaims(canonicalRoles))
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
            var permissions = AdminAppPermissionProfile.Filter(appContext, canonicalRoles, effectivePermissions);
            foreach (var p in permissions)
                list.Add(new Claim(PermissionCatalog.PermissionClaimType, p));
        }

        if (!string.IsNullOrEmpty(tenantId))
            list.Add(new Claim(ScopeCheckService.TenantIdClaim, tenantId));
        if (!string.IsNullOrEmpty(branchId))
            list.Add(new Claim(ScopeCheckService.BranchIdClaim, branchId));

        if (!string.IsNullOrEmpty(appContext))
            list.Add(new Claim(ClientAppPolicy.AppContextClaimType, appContext));

        return list;
    }
}
