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
    /// Builds DEP export with environment metadata and structural format validation.
    /// BMF root JSON remains Prüftool-compatible (no custom metadata fields).
    /// </summary>
    Task<RksvDepExportBuildResult> GenerateDepExportWithValidationAsync(
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

    /// <summary>Validates BMF DEP JSON structure (Belege-Gruppe, compact JWS, certificate fields).</summary>
    Task<RksvDepExportValidationResult> ValidateExportFormatAsync(
        string exportJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs BMF Prüftool when available. Skipped in demo mode unless <paramref name="forceRun"/> is true.
    /// </summary>
    Task<RksvDepPrueftoolResult> RunPrueftoolAsync(
        Guid cashRegisterId,
        RksvDepExportRootDto export,
        bool forceRun = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes <paramref name="exportJson"/> and runs BMF Prüftool.
    /// In demo mode Prüftool is skipped unless optional parameters are supplied via overload.
    /// </summary>
    Task<RksvDepPrueftoolResult> RunPrueftoolAsync(
        string exportJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes <paramref name="exportJson"/> and runs BMF Prüftool with register crypto material.
    /// </summary>
    Task<RksvDepPrueftoolResult> RunPrueftoolAsync(
        string exportJson,
        Guid cashRegisterId,
        bool forceRun,
        CancellationToken cancellationToken = default);
}
