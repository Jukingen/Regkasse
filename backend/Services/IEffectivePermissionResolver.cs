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
}
