using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface IImpactSimulationService
{
    /// <summary>
    /// Read-only what-if report for a proposed critical / sensitive setting change.
    /// Never writes to the database.
    /// </summary>
    Task<ImpactReport> SimulateChangeAsync(
        Guid tenantId,
        ChangeType changeType,
        object newValue,
        CancellationToken cancellationToken = default);

    Task<ImpactReport> SimulateTaxRateChangeAsync(
        Guid tenantId,
        decimal newRate,
        decimal? currentRateOverride = null,
        CancellationToken cancellationToken = default);

    Task<ImpactReport> SimulateCurrencyChangeAsync(
        Guid tenantId,
        string newCurrency,
        CancellationToken cancellationToken = default);

    Task<ImpactReport> SimulatePriceChangeAsync(
        Guid tenantId,
        IReadOnlyList<ProductPriceUpdate> updates,
        CancellationToken cancellationToken = default);
}
