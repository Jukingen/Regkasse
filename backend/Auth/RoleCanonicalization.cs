namespace KasseAPI_Final.Auth;

/// <summary>
/// Normalizes role names for token/policy (trim, empty). No legacy alias mapping.
/// </summary>
public static class RoleCanonicalization
{
    /// <summary>
    /// Returns the role trimmed, or empty if null/whitespace.
    /// </summary>
    public static string GetCanonicalRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return string.Empty;
        return role.Trim();
    }
}
