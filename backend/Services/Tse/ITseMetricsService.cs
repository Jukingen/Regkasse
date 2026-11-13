using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>TSE fleet metrics for JSON dashboards and Prometheus scrapers.</summary>
public interface ITseMetricsService
{
    Task<TseHealthMetricsSummaryDto> GetSummaryMetricsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Prometheus exposition format (text/plain openmetrics-compatible).</summary>
    Task<string> GetPrometheusMetricsAsync(CancellationToken cancellationToken = default);
}
