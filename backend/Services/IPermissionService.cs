using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Runtime permission checks and user-level override management.
/// Delegates role + override resolution to <see cref="IEffectivePermissionResolver"/>.
/// </summary>
public interface IPermissionService
{
    Task<bool> HasPermissionAsync(
        string userId,
        string permission,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserPermissionOverride>> GetUserOverridesAsync(
        string userId,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task AddOrUpdatePermissionOverrideAsync(
        string userId,
        string permission,
        bool isGranted,
        string? reason,
        DateTime? expiresAt,
        Guid? tenantId = null,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);
}
