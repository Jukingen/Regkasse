using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Generic download surface for persisted RKSV report PDFs.</summary>
[Authorize]
[ApiController]
[Route("api/admin/reports/pdf")]
[Route("api/admin/report-pdfs")]
public class ReportPdfController : ControllerBase
{
    private const string ReportPdfEntityType = "ReportPdf";

    private readonly AppDbContext _db;
    private readonly IReportPdfService _reportPdfService;
    private readonly IReportPdfStorageService _storage;
    private readonly IReportPdfCaptureService _capture;
    private readonly IDailyClosingReportService _closingReportService;
    private readonly IReceiptPdfService _receiptPdfService;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public ReportPdfController(
        AppDbContext db,
        IReportPdfService reportPdfService,
        IReportPdfStorageService storage,
        IReportPdfCaptureService capture,
        IDailyClosingReportService closingReportService,
        IReceiptPdfService receiptPdfService,
        IAuditLogService auditLogService,
        ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _reportPdfService = reportPdfService;
        _storage = storage;
        _capture = capture;
        _closingReportService = closingReportService;
        _receiptPdfService = receiptPdfService;
        _auditLogService = auditLogService;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>Download a stored RKSV report PDF by type and source entity id.</summary>
    [HttpGet("{reportType}/{reportId:guid}")]
    [HttpGet("~/api/admin/reports/{reportType}/{reportId:guid}/pdf")]
    [HttpGet("download/{reportType}/{reportId:guid}")]
    [HasPermission(AppPermissions.ReportView)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(
        string reportType,
        Guid reportId,
        [FromQuery] string? language,
        CancellationToken cancellationToken)
    {
        if (reportId == Guid.Empty)
            return BadRequest(new { message = "Report id is required" });

        if (!_tenantAccessor.TenantId.HasValue)
            return BadRequest(new { message = "Tenant context required" });

        if (!ReportPdfTypes.IsKnown(reportType))
            return BadRequest(new { message = "Unknown report type" });

        var normalizedType = ReportPdfTypes.Normalize(reportType);
        var normalizedLanguage = DailyClosingReportTemplates.NormalizeLanguage(language ?? "de");

        if (!await VerifyTenantAccessAsync(normalizedType, reportId, cancellationToken).ConfigureAwait(false))
            return NotFound();

        if (string.Equals(normalizedLanguage, "de", StringComparison.OrdinalIgnoreCase)
            && await _reportPdfService.HasPdfAsync(normalizedType, reportId, cancellationToken)
                .ConfigureAwait(false))
        {
            var pdfBytes = await _reportPdfService.GetPdfAsync(normalizedType, reportId, cancellationToken)
                .ConfigureAwait(false);
            await LogDownloadAuditAsync(normalizedType, reportId, normalizedLanguage, cancellationToken)
                .ConfigureAwait(false);
            var fileName = await _storage.ResolveDownloadFileNameAsync(normalizedType, reportId, cancellationToken)
                .ConfigureAwait(false);
            return File(pdfBytes, "application/pdf", fileName);
        }

        var download = await _storage.TryOpenDownloadAsync(
            normalizedType,
            reportId,
            normalizedLanguage,
            cancellationToken).ConfigureAwait(false);

        if (download is not null)
        {
            await LogDownloadAuditAsync(normalizedType, reportId, normalizedLanguage, cancellationToken)
                .ConfigureAwait(false);
            return File(download.Value.Stream, download.Value.ContentType, download.Value.FileName);
        }

        var actorUserId = User.GetActorUserId() ?? string.Empty;
        var isClosingEntity = await _db.DailyClosings.AsNoTracking()
            .AnyAsync(c => c.Id == reportId, cancellationToken)
            .ConfigureAwait(false);

        if (isClosingEntity)
        {
            await _capture.TryCaptureClosingReportAsync(
                reportId,
                actorUserId,
                normalizedLanguage,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var specialKind = await _db.PaymentDetails.AsNoTracking()
                .Where(p => p.Id == reportId && p.IsActive)
                .Select(p => p.RksvSpecialReceiptKind)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (specialKind is not null || normalizedType == ReportPdfTypes.Receipt)
            {
                await _capture.TryCaptureReceiptReportAsync(
                    reportId,
                    specialKind,
                    actorUserId,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (string.Equals(normalizedLanguage, "de", StringComparison.OrdinalIgnoreCase)
            && await _reportPdfService.HasPdfAsync(normalizedType, reportId, cancellationToken)
                .ConfigureAwait(false))
        {
            var pdfBytes = await _reportPdfService.GetPdfAsync(normalizedType, reportId, cancellationToken)
                .ConfigureAwait(false);
            await LogDownloadAuditAsync(normalizedType, reportId, normalizedLanguage, cancellationToken)
                .ConfigureAwait(false);
            var fileName = await _storage.ResolveDownloadFileNameAsync(normalizedType, reportId, cancellationToken)
                .ConfigureAwait(false);
            return File(pdfBytes, "application/pdf", fileName);
        }

        download = await _storage.TryOpenDownloadAsync(
            normalizedType,
            reportId,
            normalizedLanguage,
            cancellationToken).ConfigureAwait(false);

        if (download is not null)
        {
            await LogDownloadAuditAsync(normalizedType, reportId, normalizedLanguage, cancellationToken)
                .ConfigureAwait(false);
            return File(download.Value.Stream, download.Value.ContentType, download.Value.FileName);
        }

        byte[]? generatedPdf = null;
        if (isClosingEntity)
        {
            generatedPdf = await _closingReportService.TryGenerateClosingReportPdfAsync(
                reportId,
                actorUserId: null,
                normalizedLanguage,
                cancellationToken).ConfigureAwait(false);
        }
        else if (await _db.PaymentDetails.AsNoTracking()
                     .AnyAsync(p => p.Id == reportId && p.IsActive, cancellationToken)
                     .ConfigureAwait(false))
        {
            try
            {
                generatedPdf = await _receiptPdfService.GeneratePdfAsync(
                    reportId,
                    includeReprintWatermark: false,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                generatedPdf = null;
            }
        }

        if (generatedPdf is { Length: > 0 })
        {
            await LogDownloadAuditAsync(normalizedType, reportId, normalizedLanguage, cancellationToken)
                .ConfigureAwait(false);
            var fileName = await _storage.ResolveDownloadFileNameAsync(normalizedType, reportId, cancellationToken)
                .ConfigureAwait(false);
            return File(generatedPdf, "application/pdf", fileName);
        }

        var exists = await _db.ReportPdfs.AsNoTracking()
            .AnyAsync(
                p => p.ReportType == normalizedType
                     && p.ReportId == reportId
                     && p.Language == normalizedLanguage,
                cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
            return NotFound(new { message = "PDF not found" });

        return NotFound(new { message = "PDF metadata exists but file is missing on disk" });
    }

    private async Task<bool> VerifyTenantAccessAsync(
        string normalizedType,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        if (normalizedType is ReportPdfTypes.Tagesabschluss
            or ReportPdfTypes.Monatsbeleg
            or ReportPdfTypes.Jahresbeleg)
        {
            if (await _db.DailyClosings.AsNoTracking()
                    .AnyAsync(c => c.Id == reportId, cancellationToken)
                    .ConfigureAwait(false))
            {
                return true;
            }
        }

        var paymentInTenant = await (
                from payment in _db.PaymentDetails.AsNoTracking()
                where payment.Id == reportId && payment.IsActive
                join register in _db.CashRegisters on payment.CashRegisterId equals register.Id
                select payment.Id)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
        if (paymentInTenant)
            return true;

        return await _db.ReportPdfs.AsNoTracking()
            .AnyAsync(
                p => p.ReportType == normalizedType && p.ReportId == reportId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task LogDownloadAuditAsync(
        string reportType,
        Guid reportId,
        string language,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditLogService.LogSystemOperationAsync(
                    AuditLogActions.REPORT_PDF_DOWNLOADED,
                    ReportPdfEntityType,
                    User.GetActorUserId() ?? "unknown",
                    User.GetActorRole() ?? "Unknown",
                    description: $"RKSV report PDF downloaded type={reportType} id={reportId:N} language={language}",
                    actionType: AuditEventType.ReportPdfDownloaded,
                    entityId: reportId,
                    requestData: new { reportType, reportId, language })
                .ConfigureAwait(false);
        }
        catch
        {
            // Audit must not block PDF download.
        }
    }
}
