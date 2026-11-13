namespace KasseAPI_Final.Services.Reports;

public interface IReportPdfService
{
    Task<Guid> SavePdfAsync(
        string reportType,
        Guid reportId,
        byte[] pdfBytes,
        string fileName,
        Guid userId,
        CancellationToken ct = default);

    Task<byte[]> GetPdfAsync(
        string reportType,
        Guid reportId,
        CancellationToken ct = default);

    Task<bool> HasPdfAsync(
        string reportType,
        Guid reportId,
        CancellationToken ct = default);

    Task DeletePdfAsync(
        string reportType,
        Guid reportId,
        CancellationToken ct = default);
}
