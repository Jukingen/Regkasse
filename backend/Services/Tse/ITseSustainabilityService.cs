using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Indicative TSE sustainability / green-IT estimates (config rates — not certified LCA).
/// </summary>
public interface ITseSustainabilityService
{
    Task<TseSustainabilityReportDto> GetSustainabilityReportAsync(
        Guid tenantId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    Task<TseCarbonFootprintDto> CalculateCarbonFootprintAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<TseSustainabilityOptimizationResultDto> GetOptimizationSuggestionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
