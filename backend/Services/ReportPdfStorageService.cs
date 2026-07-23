using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class ReportPdfStorageService : IReportPdfStorageService
{
    private const string StorageRelativeRoot = "report-pdfs";

    private readonly AppDbContext _context;
    private readonly IHostEnvironment _environment;
    private readonly IFileNamingService _fileNaming;
    private readonly ILogger<ReportPdfStorageService> _logger;

    public ReportPdfStorageService(
        AppDbContext context,
        IHostEnvironment environment,
        IFileNamingService fileNaming,
        ILogger<ReportPdfStorageService> logger)
    {
        _context = context;
        _environment = environment;
        _fileNaming = fileNaming;
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
        var fileName = await BuildDownloadFileNameAsync(row, cancellationToken).ConfigureAwait(false);
        return (stream, fileName, "application/pdf");
    }

    public Task<string> ResolveDownloadFileNameAsync(
        string reportType,
        Guid reportId,
        CancellationToken cancellationToken = default) =>
        ResolveDownloadFileNameCoreAsync(reportType, reportId, fallbackGeneratedAt: null, cancellationToken);

    internal static string BuildRelativePath(Guid tenantId, string reportType, Guid reportId, string language) =>
        Path.Combine(
            StorageRelativeRoot,
            tenantId.ToString("N"),
            reportType,
            $"{reportId:N}_{NormalizeLanguage(language)}.pdf");

    /// <summary>Fallback when tenant/period context is unavailable.</summary>
    internal static string BuildDownloadFileName(string reportType, Guid reportId) =>
        BuildDownloadFileNameCore(
            reportType,
            tenantSlug: null,
            period: reportId.ToString("N")[..8],
            generatedAt: null);

    internal static string BuildDownloadFileName(
        string reportType,
        string? tenantSlug,
        DateTime businessDate,
        DateTime? generatedAt = null) =>
        BuildDownloadFileNameCore(
            reportType,
            tenantSlug,
            ReportExportFileNames.PeriodForReportType(reportType, businessDate),
            generatedAt);

    private static string BuildDownloadFileNameCore(
        string reportType,
        string? tenantSlug,
        string period,
        DateTime? generatedAt)
    {
        var type = ReportExportFileNames.NormalizeReportTypeLabel(reportType);
        return new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(
            $"{ReportExportFileNames.Prefix}_{type}",
            "pdf",
            additional: period,
            at: generatedAt,
            tenantSlug: tenantSlug);
    }

    private async Task<string> BuildDownloadFileNameAsync(ReportPdf row, CancellationToken cancellationToken) =>
        await ResolveDownloadFileNameCoreAsync(
                row.ReportType,
                row.ReportId,
                row.GeneratedAt,
                cancellationToken)
            .ConfigureAwait(false);

    private string BuildNamedDownload(string reportType, string? tenantSlug, DateTime businessDate, DateTime? generatedAt = null)
    {
        var type = ReportExportFileNames.NormalizeReportTypeLabel(reportType);
        var period = ReportExportFileNames.PeriodForReportType(reportType, businessDate);
        return _fileNaming.GenerateFileName(
            $"{ReportExportFileNames.Prefix}_{type}",
            "pdf",
            additional: period,
            at: generatedAt,
            tenantSlug: tenantSlug);
    }

    private async Task<string> ResolveDownloadFileNameCoreAsync(
        string reportType,
        Guid reportId,
        DateTime? fallbackGeneratedAt,
        CancellationToken cancellationToken)
    {
        var closing = await _context.DailyClosings
            .AsNoTracking()
            .Where(c => c.Id == reportId)
            .Select(c => new { c.TenantId, c.ClosingDate })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (closing is not null)
        {
            var slug = await ResolveTenantSlugAsync(closing.TenantId, cancellationToken).ConfigureAwait(false);
            return BuildNamedDownload(reportType, slug, closing.ClosingDate);
        }

        var payment = await _context.PaymentDetails
            .AsNoTracking()
            .Where(p => p.Id == reportId && p.IsActive)
            .Select(p => new { p.CreatedAt, TenantId = p.CashRegister!.TenantId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (payment is not null)
        {
            var slug = await ResolveTenantSlugAsync(payment.TenantId, cancellationToken).ConfigureAwait(false);
            return BuildNamedDownload(reportType, slug, payment.CreatedAt);
        }

        if (fallbackGeneratedAt is { } generatedAt)
        {
            var rowTenantId = await _context.ReportPdfs
                .AsNoTracking()
                .Where(r => r.ReportId == reportId && r.ReportType == reportType)
                .Select(r => (Guid?)r.TenantId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            var slug = rowTenantId is { } tid
                ? await ResolveTenantSlugAsync(tid, cancellationToken).ConfigureAwait(false)
                : null;
            return BuildNamedDownload(reportType, slug, generatedAt);
        }

        return BuildDownloadFileName(reportType, reportId);
    }

    private async Task<string?> ResolveTenantSlugAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Slug)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

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
