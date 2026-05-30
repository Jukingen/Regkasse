using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IUserPermissionOverrideService
{
    Task<IReadOnlyList<UserPermissionOverrideDto>> ListOverridesAsync(
        string userId,
        Guid? tenantScope,
        CancellationToken cancellationToken = default);

    Task<UserEffectivePermissionsDto> GetEffectivePermissionsDetailAsync(
        string userId,
        IEnumerable<string> roleNames,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    Task<UserPermissionOverrideDto?> UpsertOverrideAsync(
        string targetUserId,
        UpsertUserPermissionOverrideRequest request,
        string actorUserId,
        Guid? actorTenantScope,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteOverrideAsync(
        string targetUserId,
        Guid overrideId,
        Guid? actorTenantScope,
        CancellationToken cancellationToken = default);
}
