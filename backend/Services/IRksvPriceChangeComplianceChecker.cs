using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Pre-flight RKSV checks for catalog price / tax-group mutations.
/// </summary>
public interface IRksvPriceChangeComplianceChecker
{
    Task<RksvPriceChangeComplianceResult> CheckPriceChangeComplianceAsync(
        Guid tenantId,
        Guid productId,
        decimal newPrice,
        Guid newTaxGroupId,
        bool forceInPlaceUpdate = false,
        CancellationToken cancellationToken = default);
}
