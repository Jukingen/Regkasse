using KasseAPI_Final.DTOs.Rksv;

namespace KasseAPI_Final.Services.Rksv;

/// <summary>
/// RKSV Phase 2 Monatsbeleg (monthly closing snapshot from daily closings).
/// Distinct from the zero-value Sonderbeleg in <see cref="RksvSpecialReceiptService"/>.
/// </summary>
public interface IMonatsbelegService
{
    Task<MonatsbelegResult> CreateMonatsbelegAsync(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<MonatsbelegResult> GetMonatsbelegAsync(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<List<MonatsbelegSummary>> GetMonatsbelegHistoryAsync(
        Guid cashRegisterId,
        int? year = null,
        CancellationToken cancellationToken = default);

    Task<bool> MonatsbelegExistsAsync(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}
