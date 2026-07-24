using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// RKSV-safe product price / tax-group mutation: validates, versions, journals, and audits.
/// </summary>
public interface IPriceChangeService
{
    Task<PriceChangeResult> ChangePriceAsync(
        PriceChangeRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductPriceHistory>> GetPriceHistoryAsync(
        Guid tenantId,
        Guid productId,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<PriceChangeValidationResult> ValidatePriceChangeAsync(
        PriceChangeRequest request,
        CancellationToken cancellationToken = default);
}
