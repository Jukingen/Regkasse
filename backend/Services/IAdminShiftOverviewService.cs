using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IAdminShiftOverviewService
{
    Task<AdminShiftOverviewDto> GetOverviewAsync(
        Guid tenantId,
        Guid? cashRegisterId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int historyLimit = 200,
        CancellationToken cancellationToken = default);
}
