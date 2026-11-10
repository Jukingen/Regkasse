namespace KasseAPI_Final.Authorization;

/// <summary>
/// Privilege order for admin user and session management.
/// Platform operators (SuperAdmin / PlatformAdmin) outrank Mandanten-Admin (Manager),
/// who outrank tenant staff. There is no seeded PlatformAdmin role; the name is rejected
/// so a custom Identity role cannot be used to escalate.
/// </summary>
public static class RoleHierarchy
{
    public const string PlatformAdmin = "PlatformAdmin";

    public const string ElevatedTargetError = "User not found.";

    public const string RoleAssignmentDeniedError = "Role assignment is not permitted.";

    public static bool IsPlatformOperatorRole(string? role)
    {
        var trimmed = role?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        return string.Equals(trimmed, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, PlatformAdmin, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSuperAdminActor(string? role) =>
        string.Equals(role?.Trim(), Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase);

    /// <summary>0 = platform operator, 1 = tenant admin, 2 = staff and custom roles.</summary>
    public static int PrivilegeRank(string? role)
    {
        if (IsPlatformOperatorRole(role))
            return 0;
        if (string.Equals(role?.Trim(), Roles.Manager, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }

    /// <summary>
    /// Super Admin may change any role. A tenant admin may change users at the same rank or below
    /// and may not assign a platform-operator role.
    /// </summary>
    public static bool CanChangeUserRole(string? actorRole, string? currentRole, string? newRole)
    {
        if (IsSuperAdminActor(actorRole))
            return true;

        if (IsPlatformOperatorRole(currentRole) || IsPlatformOperatorRole(newRole))
            return false;

        var actorRank = PrivilegeRank(actorRole);
        return actorRank <= PrivilegeRank(currentRole) && actorRank <= PrivilegeRank(newRole);
    }
}
