using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// Builds an internal RKSV evidence bundle (zip) for auditor / BMF review.
/// Read-only; reuses <see cref="IRksvComplianceReportService"/> output unchanged.
/// Not a substitute for the official DEP fiscal export (<see cref="IFiscalExportService"/>).
/// </summary>
public interface IRksvEvidenceBundleService
{
    /// <summary>Builds the bundle in memory. Caller is responsible for streaming the bytes back to the user.</summary>
    /// <param name="request">Mandatory UTC range plus optional register scope.</param>
    /// <param name="generatedByUserId">User id captured into the manifest for traceability.</param>
    Task<RksvEvidenceBundleResultDto> BuildBundleAsync(
        RksvEvidenceBundleRequestDto request,
        string generatedByUserId,
        CancellationToken cancellationToken = default);
}
