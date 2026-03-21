using System.Security.Claims;

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
}
