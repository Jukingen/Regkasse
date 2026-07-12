using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services;

public interface IJahresbelegClosingService
{
    Task<JahresbelegSummaryDto> GenerateYearlySummaryPreviewAsync(
        Guid tenantId,
        Guid cashRegisterId,
        int year,
        CancellationToken cancellationToken = default);

    Task<JahresbelegClosingResult> CreateJahresbelegClosingAsync(
        string actorUserId,
        CreateJahresbelegClosingRequest request,
        CancellationToken cancellationToken = default);

    Task<JahresbelegDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JahresbelegListItemDto>> ListAsync(
        Guid? cashRegisterId,
        int? year,
        CancellationToken cancellationToken = default);

    Task<PosDailyClosingReportDto?> BuildReportDtoAsync(
        Guid jahresbelegId,
        CancellationToken cancellationToken = default);

    Task<bool> CanCreateForCurrentYearAsync(Guid cashRegisterId, CancellationToken cancellationToken = default);
}
