using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// TSE business-intelligence dashboard / report / export (diagnostic analytics only).
/// Not a DEP / Finanzamt compliance artifact.
/// </summary>
public interface ITseReportingService
{
    Task<TseBiReportDto> GenerateReportAsync(
        TseBiReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<TseBiDashboardDto> GetDashboardDataAsync(
        Guid tenantId,
        int lookbackDays = 30,
        CancellationToken cancellationToken = default);

    Task<TseBiExportResultDto> ExportReportAsync(
        TseBiExportRequestDto request,
        CancellationToken cancellationToken = default);
}
