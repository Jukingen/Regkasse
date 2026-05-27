using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Security;

/// <summary>
/// Central JWT / claims extraction for authenticated actor. Order matches issued tokens from <see cref="KasseAPI_Final.Services.TokenClaimsService"/>.
/// </summary>
public static class PrincipalActorExtensions
{
    private const string LegacyUserIdClaimType = "user_id";

    /// <summary>
    /// Resolves application user id: explicit <c>userId</c>, then standard JWT / Identity claim types (inbound mapping varies).
    /// </summary>
    public static string? GetActorUserId(this ClaimsPrincipal? principal)
    {
        if (principal?.Identities == null)
            return null;

        static string? FirstNonEmpty(ClaimsPrincipal p, string claimType)
        {
            var v = p.FindFirst(claimType)?.Value;
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        return FirstNonEmpty(principal, "userId")
               ?? FirstNonEmpty(principal, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
               ?? FirstNonEmpty(principal, JwtRegisteredClaimNames.Sub)
               ?? FirstNonEmpty(principal, LegacyUserIdClaimType)
               ?? FirstNonEmpty(principal, ClaimTypes.NameIdentifier);
    }

    public static string? GetActorRole(this ClaimsPrincipal? principal)
    {
        if (principal == null)
            return null;

        return principal.FindFirst("role")?.Value
               ?? principal.FindFirst(ClaimTypes.Role)?.Value;
    }

    public static string? GetActorEmail(this ClaimsPrincipal? principal)
    {
        if (principal == null)
            return null;

        return principal.FindFirst(ClaimTypes.Email)?.Value
               ?? principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
    }

    /// <summary>
    /// Permission claim eşleşmesi (RolePermissionMatrix ile üretilen JWT permission claim’leri).
    /// </summary>
    public static bool HasPermissionClaim(this ClaimsPrincipal? principal, string permission)
    {
        if (principal == null || string.IsNullOrEmpty(permission)) return false;
        return principal.HasClaim(PermissionCatalog.PermissionClaimType, permission);
    }
}
