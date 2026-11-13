using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Mandanten-Admin (Manager) read-only TSE self-service portal.
/// Ambient tenant only — never Super Admin cross-tenant fleet ops.
/// </summary>
public interface ITenantTsePortalService
{
    Task<TenantTseStatusDto> GetStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TenantTseHealthHistoryDto> GetHealthHistoryAsync(
        Guid tenantId,
        int days = 30,
        CancellationToken cancellationToken = default);
}
