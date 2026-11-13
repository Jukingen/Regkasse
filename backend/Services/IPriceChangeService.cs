using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// RKSV-safe product price / tax-group mutation: validates, versions, journals, and audits.
/// Products with prior fiscal sales are superseded by a new catalog product row.
/// </summary>
public interface IPriceChangeService
{
    Task<PriceChangeResult> ChangePriceAsync(
        PriceChangeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives the original product and creates a successor catalog row with the new price/tax.
    /// Used when the product already has RKSV / sales history.
    /// </summary>
    Task<Product> CreateNewProductVersionAsync(
        Guid productId,
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
