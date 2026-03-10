using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Auth;

/// <summary>
/// Normalizes role names for token/policy (trim, empty). No legacy alias mapping; canonical roles from Authorization.Roles.
/// </summary>
public static class RoleCanonicalization
{
    /// <summary>
    /// Returns the role trimmed, or empty if null/whitespace. No legacy alias mapping; use Roles from Authorization.
    /// </summary>
    public static string GetCanonicalRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return string.Empty;
        var trimmed = role.Trim();
        // Legacy Admin role merged into SuperAdmin; normalize for token/policy until all JWTs refreshed.
        if (string.Equals(trimmed, "Admin", StringComparison.OrdinalIgnoreCase))
            return Roles.SuperAdmin;
        return trimmed;
    }
}
