using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface ITaxReportService
{
    Task<TaxReport> GetReportAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaxTrendPoint>> GetTrendAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        string granularity = "day",
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportCsvAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default);
}
