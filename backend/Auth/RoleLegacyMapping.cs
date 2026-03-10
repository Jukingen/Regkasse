using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Auth;

/// <summary>
/// Central map from legacy role names to canonical targets for migrations and token normalization.
/// Extend when deprecating additional legacy names; keep in sync with data migrations.
/// </summary>
public static class RoleLegacyMapping
{
    /// <summary>
    /// Legacy name -> canonical role name to assign after migration. Empty value means "map via RoleCanonicalization only" (no DB role row).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> LegacyToCanonicalRole = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Migrated by 20260310175350: Admin users -> SuperAdmin; Admin role dropped.
        ["Admin"] = Roles.SuperAdmin,
        // Migrated by CanonicalizeLegacyRoleNames / DropAdministratorRole.
        ["Administrator"] = Roles.SuperAdmin,
        // Migrated by 20260310180000: Demo role removed; users -> Cashier + IsDemo.
        ["Demo"] = Roles.Cashier,
    };

    /// <summary>
    /// If legacy name is known, returns canonical role name for reassignment; otherwise null.
    /// </summary>
    public static string? TryGetCanonicalReplacement(string? legacyRoleName)
    {
        if (string.IsNullOrWhiteSpace(legacyRoleName)) return null;
        return LegacyToCanonicalRole.TryGetValue(legacyRoleName.Trim(), out var canonical) ? canonical : null;
    }
}
