using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IDailyClosingReportService
{
    /// <summary>Builds a localized PDF for an in-memory daily closing report snapshot.</summary>
    byte[] GenerateDailyReportPdf(PosDailyClosingReportDto report, string language = "de");

    /// <summary>
    /// Loads a persisted daily closing for the caller's shift and returns a localized PDF, or null when not found.
    /// </summary>
    Task<byte[]?> TryGenerateStoredDailyReportPdfAsync(
        Guid dailyClosingId,
        string cashierUserId,
        string language = "de",
        CancellationToken cancellationToken = default);
}
