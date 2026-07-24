using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Super Admin TSE signing-capacity planning from receipt volume trends (not fiscal evidence).
/// </summary>
public interface ITseCapacityPlanningService
{
    Task<TseCapacityReportDto> GetCapacityReportAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TseForecastResultDto> ForecastCapacityAsync(
        Guid tenantId,
        int forecastDays = 30,
        CancellationToken cancellationToken = default);

    Task<TseCapacityAlertDto> CheckCapacityAlertsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
