using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Reports;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class ReportPdfStorageService : IReportPdfStorageService
{
    private const string StorageRelativeRoot = "report-pdfs";

    private readonly AppDbContext _context;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ReportPdfStorageService> _logger;

    public ReportPdfStorageService(
        AppDbContext context,
        IHostEnvironment environment,
        ILogger<ReportPdfStorageService> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    public async Task<ReportPdf> SaveAsync(
        ReportPdfStoreRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes must not be empty.", nameof(request));

        var language = NormalizeLanguage(request.Language);
        var relativePath = BuildRelativePath(
            request.TenantId,
            request.ReportType,
            request.ReportId,
            language);
        var absolutePath = Path.Combine(_environment.ContentRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await File.WriteAllBytesAsync(absolutePath, request.PdfBytes, cancellationToken).ConfigureAwait(false);

        var normalizedPath = relativePath.Replace('\\', '/');
        var existing = await _context.ReportPdfs
            .FirstOrDefaultAsync(
                r => r.ReportType == request.ReportType
                     && r.ReportId == request.ReportId
                     && r.Language == language,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            existing = new ReportPdf
            {
                TenantId = request.TenantId,
                ReportType = request.ReportType,
                ReportId = request.ReportId,
                Language = language,
                GeneratedByUserId = request.GeneratedByUserId,
            };
            _context.ReportPdfs.Add(existing);
        }

        existing.PdfPath = normalizedPath;
        existing.FileSizeBytes = request.PdfBytes.LongLength;
        existing.GeneratedAt = DateTime.UtcNow;
        existing.GeneratedByUserId = request.GeneratedByUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Stored RKSV report PDF type={ReportType} reportId={ReportId} bytes={FileSize}",
            request.ReportType,
            request.ReportId,
            existing.FileSizeBytes);

        return existing;
    }

    public async Task<byte[]?> TryLoadBytesAsync(
        string reportType,
        Guid reportId,
        string language = "de",
        CancellationToken cancellationToken = default)
    {
        var row = await FindRowAsync(reportType, reportId, language, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return null;

        var absolutePath = ResolveAbsolutePath(row.PdfPath);
        if (!File.Exists(absolutePath))
            return null;

        return await File.ReadAllBytesAsync(absolutePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasStoredPdfAsync(
        string reportType,
        Guid reportId,
        string language = "de",
        CancellationToken cancellationToken = default)
    {
        var row = await FindRowAsync(reportType, reportId, language, cancellationToken).ConfigureAwait(false);
        return row is not null && File.Exists(ResolveAbsolutePath(row.PdfPath));
    }

    public async Task<IReadOnlySet<Guid>> GetStoredReportIdsAsync(
        string reportType,
        IReadOnlyCollection<Guid> reportIds,
        string language = "de",
        CancellationToken cancellationToken = default)
    {
        if (reportIds.Count == 0)
            return new HashSet<Guid>();

        var normalizedLanguage = NormalizeLanguage(language);
        var ids = reportIds.Distinct().ToList();
        var rows = await _context.ReportPdfs.AsNoTracking()
            .Where(r => r.ReportType == reportType && r.Language == normalizedLanguage && ids.Contains(r.ReportId))
            .Select(r => new { r.ReportId, r.PdfPath })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var stored = new HashSet<Guid>();
        foreach (var row in rows)
        {
            if (File.Exists(ResolveAbsolutePath(row.PdfPath)))
                stored.Add(row.ReportId);
        }

        return stored;
    }

    public async Task<(Stream Stream, string FileName, string ContentType)?> TryOpenDownloadAsync(
        string reportType,
        Guid reportId,
        string language = "de",
        CancellationToken cancellationToken = default)
    {
        var row = await FindRowAsync(reportType, reportId, language, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return null;

        var absolutePath = ResolveAbsolutePath(row.PdfPath);
        if (!File.Exists(absolutePath))
            return null;

        var stream = File.OpenRead(absolutePath);
        var fileName = BuildDownloadFileName(reportType, reportId);
        return (stream, fileName, "application/pdf");
    }

    internal static string BuildRelativePath(Guid tenantId, string reportType, Guid reportId, string language) =>
        Path.Combine(
            StorageRelativeRoot,
            tenantId.ToString("N"),
            reportType,
            $"{reportId:N}_{NormalizeLanguage(language)}.pdf");

    internal static string BuildDownloadFileName(string reportType, Guid reportId) =>
        $"RKSV-{reportType}-{reportId:N}.pdf";

    private async Task<ReportPdf?> FindRowAsync(
        string reportType,
        Guid reportId,
        string language,
        CancellationToken cancellationToken)
    {
        if (reportId == Guid.Empty || string.IsNullOrWhiteSpace(reportType))
            return null;

        return await _context.ReportPdfs.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.ReportType == reportType
                     && r.ReportId == reportId
                     && r.Language == NormalizeLanguage(language),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private string ResolveAbsolutePath(string relativePath) =>
        Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(_environment.ContentRootPath, relativePath);

    private static string NormalizeLanguage(string? language) =>
        DailyClosingReportTemplates.NormalizeLanguage(language ?? "de");
}
