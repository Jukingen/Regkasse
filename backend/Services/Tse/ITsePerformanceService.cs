using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Aggregates TSE health-probe latency/success from <c>tse_device_health_samples</c>
/// (not payment-path <c>PaymentMetrics</c>).
/// </summary>
public interface ITsePerformanceService
{
    Task<TsePerformanceMetricsDto> GetPerformanceMetricsAsync(
        Guid deviceId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates recent probe anomalies (slow latency / high error rate) and optionally
    /// publishes activity alerts.
    /// </summary>
    Task<TsePerformanceAlertDto> CheckPerformanceAnomaliesAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>Background sweep over active primary/failover devices.</summary>
    Task ProcessPerformanceAnomaliesAsync(CancellationToken cancellationToken = default);
}
