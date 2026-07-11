using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IDailyClosingReportService
{
    /// <summary>Builds a localized PDF for an in-memory closing report snapshot.</summary>
    byte[] GenerateDailyReportPdf(PosDailyClosingReportDto report, string language = "de");

    /// <summary>
    /// Loads a persisted daily closing for the caller's shift and returns a localized PDF, or null when not found.
    /// </summary>
    Task<byte[]?> TryGenerateStoredDailyReportPdfAsync(
        Guid dailyClosingId,
        string cashierUserId,
        string language = "de",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant-scoped closing PDF. When <paramref name="actorUserId"/> is set, limits access to owner/shift cashier.
    /// </summary>
    Task<byte[]?> TryGenerateClosingReportPdfAsync(
        Guid closingId,
        string? actorUserId,
        string language = "de",
        CancellationToken cancellationToken = default);
}
