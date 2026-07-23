using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.ActivityReports;

public interface IActivityAnomalyService
{
    Task<IReadOnlyList<ActivityAnomalyDto>> DetectAnomaliesAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<ActivitySummaryDto> activities,
        CancellationToken cancellationToken = default);
}
