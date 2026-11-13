using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Heuristic TSE / POS user-behavior analytics from sessions and audit events (diagnostic only).
/// </summary>
public interface ITseUserAnalyticsService
{
    Task<TseUserBehaviorReportDto> GenerateUserReportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<TseFeatureUsageReportDto> GetFeatureUsageReportAsync(
        Guid? tenantId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    Task<TseCohortAnalysisResultDto> PerformCohortAnalysisAsync(
        Guid? tenantId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);
}
