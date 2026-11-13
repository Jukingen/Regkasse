using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Limits;

/// <summary>Admin dashboard for tenant-limit usage, critical users, and recent alerts.</summary>
public interface ITenantLimitDashboardService
{
    Task<LimitDashboardDto> GetDashboardAsync(
        Guid tenantId,
        string? readerUserId,
        CancellationToken cancellationToken = default);

    Task<LimitDashboardDto> GetDashboardForAllTenantsAsync(
        string? readerUserId,
        CancellationToken cancellationToken = default);
}
