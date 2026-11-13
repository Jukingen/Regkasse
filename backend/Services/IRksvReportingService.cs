using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// RKSV reporting that only uses tax rates / amounts frozen at receipt time
/// (<see cref="ReceiptTaxLine"/>), never current product catalog prices.
/// </summary>
public interface IRksvReportingService
{
    Task<RksvReport> GenerateHistoricalReportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<TaxBreakdown> GetTaxBreakdownForPeriodAsync(
        Guid tenantId,
        DateTime dateUtc,
        CancellationToken cancellationToken = default);

    Task<PriceHistoryReport> GetPriceHistoryForProductAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken cancellationToken = default);
}
