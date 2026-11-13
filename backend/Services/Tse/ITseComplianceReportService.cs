using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Composes tenant TSE/RKSV compliance snapshots from health, signature-chain continuity,
/// and receipt signature coverage. Read-only; not a legal Finanzamt proof.
/// </summary>
public interface ITseComplianceReportService
{
    /// <param name="fromUtc">Inclusive period start (UTC).</param>
    /// <param name="toUtc">Exclusive period end (UTC).</param>
    Task<TseComplianceReportDto> GenerateComplianceReportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Rolling lookback status (default 7 days).</summary>
    Task<TseComplianceStatusDto> GetComplianceStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Audit-readiness dashboard: score, certificates, transactions, audit trail.</summary>
    Task<TseComplianceDashboardDto> GetComplianceDashboardAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>JSON export payload for the compliance dashboard (UTF-8 bytes).</summary>
    Task<(byte[] Content, string FileName)> ExportComplianceReportAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}
