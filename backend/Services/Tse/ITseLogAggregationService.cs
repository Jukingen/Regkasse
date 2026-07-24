using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Centralized read-only aggregation of TSE operational logs from existing sources (not a new fiscal log store).
/// </summary>
public interface ITseLogAggregationService
{
    Task<TseLogAggregationResultDto> AggregateLogsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<TseLogSearchResultDto> SearchLogsAsync(
        TseLogSearchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<TseLogAnalysisReportDto> AnalyzeLogsAsync(
        Guid tenantId,
        TseLogAnalysisRequestDto request,
        CancellationToken cancellationToken = default);
}
