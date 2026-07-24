using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Heuristic predictive health analytics from health samples, certificates, and failover history.
/// </summary>
public interface ITsePredictiveAnalyticsService
{
    Task<TsePredictionResultDto> PredictFailureAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseRiskFactorDto>> IdentifyRiskFactorsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TseHealthPredictionDto> ForecastHealthAsync(
        Guid deviceId,
        int days,
        CancellationToken cancellationToken = default);
}
