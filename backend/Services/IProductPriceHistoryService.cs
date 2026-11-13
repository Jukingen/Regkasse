using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface IProductPriceHistoryService
{
    /// <summary>
    /// Closes the previous active history/version (if any) and appends a new price/tax change journal entry.
    /// No-op (returns null) when price, tax group, and tax rate are unchanged.
    /// </summary>
    Task<ProductPriceHistory?> RecordChangeAsync(
        Guid tenantId,
        Guid productId,
        decimal oldPrice,
        decimal newPrice,
        Guid oldTaxGroupId,
        Guid newTaxGroupId,
        decimal oldTaxRate,
        decimal newTaxRate,
        Guid changedBy,
        string? reason = null,
        bool isRksvCompliant = true,
        string? rksvNote = null,
        bool saveChanges = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds the initial current <see cref="ProductPriceVersion"/> (and open history interval) for a new product.
    /// </summary>
    Task EnsureInitialVersionAsync(
        Guid tenantId,
        Guid productId,
        decimal price,
        Guid taxGroupId,
        decimal taxRate,
        Guid changedBy,
        string? reason = null,
        bool saveChanges = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductPriceHistoryItemDto>> GetHistoryAsync(
        Guid tenantId,
        Guid? productId = null,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductPriceVersionItemDto>> GetVersionsAsync(
        Guid tenantId,
        Guid productId,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<ProductPriceVersionItemDto?> GetCurrentVersionAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken cancellationToken = default);
}
