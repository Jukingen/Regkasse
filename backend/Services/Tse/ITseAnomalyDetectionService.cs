using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Statistical TSE anomaly detection (baseline mean + deviation %).
/// Diagnostic only — not a certified ML / Finanzamt model.
/// </summary>
public interface ITseAnomalyDetectionService
{
    Task<TseAnomalyResultDto> DetectAnomaliesAsync(
        Guid tenantId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseAnomalyResultDto> DetectAnomaliesForDeviceAsync(
        Guid deviceId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseAnomalyReportDto> GenerateAnomalyReportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<bool> IsAnomalyDetectedAsync(
        Guid tenantId,
        string metricName,
        double value,
        CancellationToken cancellationToken = default);

    Task<TseAnomalyDashboardDto> GetDashboardAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TseAnomalyDto> ResolveAnomalyAsync(
        Guid anomalyId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);
}
