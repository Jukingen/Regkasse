using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// Diagnostic RKSV compliance test report builder. Read-only; no DB mutations.
/// Five checks: special receipts list, signature chain continuity, receipt-number gaps,
/// TSE signature presence, RKSV QR payload format validation. Not a legal RKSV proof.
/// </summary>
public interface IRksvComplianceReportService
{
    /// <param name="cashRegisterId">When provided, scopes the report to a single cash register.</param>
    /// <param name="fromUtc">Inclusive lower bound on <c>receipts.issued_at</c> (UTC). Null = unbounded.</param>
    /// <param name="toUtc">Exclusive upper bound on <c>receipts.issued_at</c> (UTC). Null = unbounded.</param>
    Task<RksvComplianceReportDto> BuildReportAsync(
        Guid? cashRegisterId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);
}
