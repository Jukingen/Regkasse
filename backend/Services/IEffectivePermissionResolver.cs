namespace KasseAPI_Final.Services;

/// <summary>
/// Resolves effective permissions for a user: role permissions plus user-level grant/deny overrides.
/// </summary>
public interface IEffectivePermissionResolver
{
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        string userId,
        IEnumerable<string> roleNames,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dry-run: apply user overrides on top of a proposed role permission set (no DB role lookup).
    /// </summary>
    Task<IReadOnlySet<string>> GetEffectivePermissionsWithRoleOverrideAsync(
        string userId,
        IReadOnlySet<string> proposedRolePermissions,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}
