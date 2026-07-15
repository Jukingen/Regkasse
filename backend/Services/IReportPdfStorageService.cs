using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public sealed class ReportPdfStoreRequest
{
    public required Guid TenantId { get; init; }
    public required string ReportType { get; init; }
    public required Guid ReportId { get; init; }
    public required byte[] PdfBytes { get; init; }
    public required Guid GeneratedByUserId { get; init; }
    public string Language { get; init; } = "de";
}

public interface IReportPdfStorageService
{
    Task<ReportPdf> SaveAsync(ReportPdfStoreRequest request, CancellationToken cancellationToken = default);

    Task<byte[]?> TryLoadBytesAsync(
        string reportType,
        Guid reportId,
        string language = "de",
        CancellationToken cancellationToken = default);

    Task<bool> HasStoredPdfAsync(
        string reportType,
        Guid reportId,
        string language = "de",
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetStoredReportIdsAsync(
        string reportType,
        IReadOnlyCollection<Guid> reportIds,
        string language = "de",
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string FileName, string ContentType)?> TryOpenDownloadAsync(
        string reportType,
        Guid reportId,
        string language = "de",
        CancellationToken cancellationToken = default);
}
