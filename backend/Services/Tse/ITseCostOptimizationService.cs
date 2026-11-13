using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Indicative TSE cost monitoring and savings recommendations (config-based rates; not billing).
/// </summary>
public interface ITseCostOptimizationService
{
    Task<TseCostReportDto> GetCostReportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseCostSavingRecommendationDto>> GetOptimizationRecommendationsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TseCostAlertDto> CheckCostAnomaliesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
