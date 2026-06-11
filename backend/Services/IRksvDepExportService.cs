using KasseAPI_Final.Models.Export;

namespace KasseAPI_Final.Services;

/// <summary>
/// Builds the official BMF RKSV DEP JSON export (§7 Signaturjournal) for one cash register and UTC period.
/// </summary>
public interface IRksvDepExportService
{
    Task<RksvDepExportRootDto> GenerateDepExportAsync(
        Guid cashRegisterId,
        DateTime fromUtc,
        DateTime toUtc,
        bool includeSpecialReceipts = true,
        bool includeDailyClosings = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the BMF Prüftool cryptographic material container for one cash register
    /// (AES turnover key + signing certificates referenced by signed receipts).
    /// </summary>
    Task<CryptoMaterialDto> GenerateCryptoMaterialAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);
}
