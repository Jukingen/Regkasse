using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface ITaxHistoryService
{
    Task<TaxHistory?> RecordChangeAsync(
        Guid tenantId,
        Guid productId,
        Guid taxGroupId,
        decimal oldRate,
        decimal newRate,
        Guid changedBy,
        string? reason = null,
        string? invoiceNumber = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaxHistoryItemDto>> GetHistoryAsync(
        Guid tenantId,
        Guid? productId = null,
        int take = 100,
        CancellationToken cancellationToken = default);
}
