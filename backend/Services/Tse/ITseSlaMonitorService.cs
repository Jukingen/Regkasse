using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Super Admin TSE SLA monitoring from health samples + signed receipt success (not a fiscal certificate).
/// </summary>
public interface ITseSlaMonitorService
{
    Task<TseSlaReportDto> GetSlaReportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<TseSlaStatusDto> GetCurrentSlaStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TseSlaAlertDto> CheckSlaViolationsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
