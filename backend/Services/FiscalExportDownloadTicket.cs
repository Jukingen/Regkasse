using KasseAPI_Final.Models.Export;

namespace KasseAPI_Final.Services;

/// <summary>Short-lived server-side fiscal export artifact for deferred download.</summary>
public sealed class FiscalExportDownloadTicket
{
    public required FiscalExportJsonEnvelopeDto Envelope { get; init; }

    /// <summary>Normalized format: jsondownload | pdf</summary>
    public required string NormalizedExportFormat { get; init; }

    public required string PreparedForUserId { get; init; }
}
