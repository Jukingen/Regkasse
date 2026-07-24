using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface ITaxGroupStatsService
{
    /// <summary>
    /// Product counts per tax group plus period gross Umsatz attributed by matching MwSt rate.
    /// </summary>
    Task<TaxGroupStatsReport> GetStatsAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default);
}
