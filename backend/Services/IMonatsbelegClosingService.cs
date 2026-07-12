using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services;

public interface IMonatsbelegClosingService
{
    Task<MonatsbelegSummaryDto> GenerateMonthlySummaryPreviewAsync(
        Guid tenantId,
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<MonatsbelegClosingResult> CreateMonatsbelegClosingAsync(
        string actorUserId,
        CreateMonatsbelegClosingRequest request,
        CancellationToken cancellationToken = default);

    Task<MonatsbelegDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonatsbelegListItemDto>> ListAsync(
        Guid? cashRegisterId,
        int? year,
        int? month,
        CancellationToken cancellationToken = default);

    Task<PosDailyClosingReportDto?> BuildReportDtoAsync(
        Guid monatsbelegId,
        CancellationToken cancellationToken = default);

    Task<bool> CanCreateForCurrentMonthAsync(Guid cashRegisterId, CancellationToken cancellationToken = default);
}
