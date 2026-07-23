using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IPermissionPackageService
{
    Task EnsureSeedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionPackageDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<PermissionPackageDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PermissionPackageDto?> CreateAsync(
        UpsertPermissionPackageRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<PermissionPackageDto?> UpdateAsync(
        Guid id,
        UpsertPermissionPackageRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Code, string? Error)> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Code, string? Error)> AddPackageToRoleAsync(
        string roleName,
        Guid packageId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Code, string? Error)> RemovePackageFromRoleAsync(
        string roleName,
        Guid packageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleAssignedPackageDto>> ListAssignedPackagesForRoleAsync(
        string roleName,
        CancellationToken cancellationToken = default);
}
