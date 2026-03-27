using System.Security.Claims;
using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Security;

/// <summary>
/// Central JWT / claims extraction for authenticated actor (matches legacy user_id fallback).
/// </summary>
public static class PrincipalActorExtensions
{
    private const string LegacyUserIdClaimType = "user_id";

    public static string? GetActorUserId(this ClaimsPrincipal? principal) =>
        principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? principal?.FindFirst(LegacyUserIdClaimType)?.Value;

    public static string? GetActorRole(this ClaimsPrincipal? principal) =>
        principal?.FindFirst(ClaimTypes.Role)?.Value;

    /// <summary>
    /// Permission claim eşleşmesi (RolePermissionMatrix ile üretilen JWT permission claim’leri).
    /// </summary>
    public static bool HasPermissionClaim(this ClaimsPrincipal? principal, string permission)
    {
        if (principal == null || string.IsNullOrEmpty(permission)) return false;
        return principal.HasClaim(PermissionCatalog.PermissionClaimType, permission);
    }
}
