using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>Tenant TSE fleet health report + historical score trends.</summary>
public interface ITseHealthTrendService
{
    Task<TseHealthReportDto> GenerateHealthReportAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Health score samples for the tenant (optionally one device) over the last <paramref name="days"/> days.
    /// </summary>
    Task<IReadOnlyList<TseHealthTrendPointDto>> GetHealthTrendAsync(
        Guid tenantId,
        int days,
        Guid? deviceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist a throttled health sample after a device probe (called from health check service).
    /// </summary>
    Task TryRecordSampleAsync(
        Models.TseDevice device,
        int healthScore,
        Models.TseHealthStatus status,
        string? message,
        DateTime checkedAtUtc,
        int? responseTimeMs = null,
        CancellationToken cancellationToken = default);
}
