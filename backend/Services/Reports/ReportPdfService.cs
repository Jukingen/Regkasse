using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services.Reports;

/// <summary>
/// Tenant-scoped facade for persisted RKSV report PDFs.
/// Delegates save/load/has to <see cref="IReportPdfStorageService"/>; handles delete locally.
/// </summary>
public sealed class ReportPdfService : IReportPdfService
{
    private const string DefaultLanguage = "de";

    private readonly AppDbContext _context;
    private readonly IReportPdfStorageService _storage;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ReportPdfService> _logger;

    public ReportPdfService(
        AppDbContext context,
        IReportPdfStorageService storage,
        ISettingsTenantResolver tenantResolver,
        IHostEnvironment environment,
        ILogger<ReportPdfService> logger)
    {
        _context = context;
        _storage = storage;
        _tenantResolver = tenantResolver;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> SavePdfAsync(
        string reportType,
        Guid reportId,
        byte[] pdfBytes,
        string fileName,
        Guid userId,
        CancellationToken ct = default)
    {
        var normalizedType = NormalizeReportType(reportType);
        if (reportId == Guid.Empty)
            throw new ArgumentException("Report id is required.", nameof(reportId));
        if (pdfBytes is null or { Length: 0 })
            throw new ArgumentException("PDF bytes must not be empty.", nameof(pdfBytes));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(ct).ConfigureAwait(false);
        var row = await _storage.SaveAsync(
            new ReportPdfStoreRequest
            {
                TenantId = tenantId,
                ReportType = normalizedType,
                ReportId = reportId,
                PdfBytes = pdfBytes,
                GeneratedByUserId = userId,
                Language = DefaultLanguage,
            },
            ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Saved RKSV report PDF type={ReportType} reportId={ReportId} fileName={FileName} bytes={FileSize}",
            normalizedType,
            reportId,
            fileName.Trim(),
            pdfBytes.LongLength);

        return row.Id;
    }

    /// <inheritdoc />
    public async Task<byte[]> GetPdfAsync(
        string reportType,
        Guid reportId,
        CancellationToken ct = default)
    {
        var normalizedType = NormalizeReportType(reportType);
        if (reportId == Guid.Empty)
            throw new ArgumentException("Report id is required.", nameof(reportId));

        var bytes = await _storage.TryLoadBytesAsync(
            normalizedType,
            reportId,
            DefaultLanguage,
            ct).ConfigureAwait(false);

        if (bytes is null or { Length: 0 })
        {
            throw new KeyNotFoundException(
                $"PDF not found for report type={normalizedType} id={reportId:N}.");
        }

        return bytes;
    }

    /// <inheritdoc />
    public Task<bool> HasPdfAsync(
        string reportType,
        Guid reportId,
        CancellationToken ct = default)
    {
        if (reportId == Guid.Empty || string.IsNullOrWhiteSpace(reportType))
            return Task.FromResult(false);

        var normalizedType = ReportPdfTypes.Normalize(reportType);
        if (!ReportPdfTypes.IsKnown(normalizedType))
            return Task.FromResult(false);

        return _storage.HasStoredPdfAsync(normalizedType, reportId, DefaultLanguage, ct);
    }

    /// <inheritdoc />
    public async Task DeletePdfAsync(
        string reportType,
        Guid reportId,
        CancellationToken ct = default)
    {
        var normalizedType = NormalizeReportType(reportType);
        if (reportId == Guid.Empty)
            return;

        var row = await _context.ReportPdfs
            .FirstOrDefaultAsync(
                r => r.ReportType == normalizedType
                     && r.ReportId == reportId
                     && r.Language == DefaultLanguage,
                ct)
            .ConfigureAwait(false);

        if (row is null)
            return;

        var absolutePath = ResolveAbsolutePath(row.PdfPath);
        if (File.Exists(absolutePath))
        {
            try
            {
                File.Delete(absolutePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to delete report PDF file path={PdfPath} type={ReportType} reportId={ReportId}",
                    row.PdfPath,
                    normalizedType,
                    reportId);
            }
        }

        _context.ReportPdfs.Remove(row);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Deleted RKSV report PDF type={ReportType} reportId={ReportId}",
            normalizedType,
            reportId);
    }

    private static string NormalizeReportType(string reportType)
    {
        if (string.IsNullOrWhiteSpace(reportType))
            throw new ArgumentException("Report type is required.", nameof(reportType));

        var normalized = ReportPdfTypes.Normalize(reportType);
        if (!ReportPdfTypes.IsKnown(normalized))
            throw new ArgumentException($"Unknown report type: {reportType}", nameof(reportType));

        return normalized;
    }

    private string ResolveAbsolutePath(string relativePath) =>
        Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(_environment.ContentRootPath, relativePath);
}
