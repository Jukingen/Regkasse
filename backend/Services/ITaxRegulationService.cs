using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface ITaxRegulationService
{
    Task<TaxRegulation> GetCurrentRegulationAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<TaxRegulation>> GetRegulationHistoryAsync(CancellationToken cancellationToken = default);

    Task<bool> IsTaxRateValidAsync(decimal rate, CancellationToken cancellationToken = default);

    Task<TaxChangeImpact> GetTaxChangeImpactAsync(
        Guid tenantId,
        decimal oldRate,
        decimal newRate,
        CancellationToken cancellationToken = default);
}
