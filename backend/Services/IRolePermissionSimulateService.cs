using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IRolePermissionSimulateService
{
    Task<RolePermissionSimulateResultDto> SimulateAsync(
        string roleName,
        IReadOnlyList<string> proposedPermissions,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
