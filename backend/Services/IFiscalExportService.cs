using KasseAPI_Final.Models.Export;

namespace KasseAPI_Final.Services;

/// <summary>
/// Builds a DEP-like fiscal export package: receipts, TSE/RKSV signatures, chain state, and closings.
/// </summary>
public interface IFiscalExportService
{
    /// <summary>
    /// Loads fiscal data for the register and UTC period. Throws if cash register is missing.
    /// </summary>
    Task<FiscalExportPackageDto> BuildExportAsync(
        Guid cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        bool includeCsv,
        CancellationToken cancellationToken = default);
}
