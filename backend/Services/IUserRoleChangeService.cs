using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public sealed class UserRoleChangeResult
{
    public bool RoleChanged { get; init; }
    public string? PreviousRole { get; init; }
    public string? NewRole { get; init; }
    public bool PreservePreviousPermissions { get; init; }
    public int OverridesCreatedOrUpdated { get; init; }
}

/// <summary>
/// Centralized Identity role swap with optional permission preservation via user overrides.
/// </summary>
public interface IUserRoleChangeService
{
    Task<(UserRoleChangeResult Result, string? Error)> ChangeUserRoleAsync(
        ApplicationUser user,
        string newRole,
        bool preservePreviousPermissions,
        string actorUserId,
        string actorRole,
        Guid? tenantIdForAudit,
        CancellationToken cancellationToken = default);
}
