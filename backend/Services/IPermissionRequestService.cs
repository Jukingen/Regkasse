using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IPermissionRequestService
{
    Task<PermissionRequestMutationResult> CreateAsync(
        string requesterUserId,
        Guid? tenantId,
        CreatePermissionRequestBody body,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionRequestDto>> ListMineAsync(
        string requesterUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionRequestDto>> ListPendingAsync(
        CancellationToken cancellationToken = default);

    Task<PermissionRequestMutationResult> ApproveAsync(
        Guid requestId,
        string resolverUserId,
        ResolvePermissionRequestBody? body,
        CancellationToken cancellationToken = default);

    Task<PermissionRequestMutationResult> RejectAsync(
        Guid requestId,
        string resolverUserId,
        ResolvePermissionRequestBody? body,
        CancellationToken cancellationToken = default);

    Task<PermissionRequestStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
}
