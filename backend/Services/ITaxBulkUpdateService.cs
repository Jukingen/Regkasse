using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface ITaxBulkUpdateService
{
    Task<TaxBulkUpdateResultDto> UpdateTaxForProductsAsync(
        Guid tenantId,
        Guid oldTaxGroupId,
        Guid newTaxGroupId,
        Guid changedBy,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign <paramref name="taxGroupId"/> to the given product ids (tenant-scoped).
    /// Writes <see cref="Models.TaxHistory"/> only when the group/rate actually changes.
    /// </summary>
    Task<TaxApplyToProductsResultDto> ApplyTaxGroupToProductsAsync(
        Guid tenantId,
        Guid taxGroupId,
        IReadOnlyList<Guid> productIds,
        Guid changedBy,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
