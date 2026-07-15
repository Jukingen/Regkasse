using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Persisted RKSV report PDF download (Nachdruck / stored original).</summary>
[Authorize]
[ApiController]
[Route("api/admin/report-pdfs")]
[Produces("application/json")]
public class AdminReportPdfController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IReportPdfStorageService _storage;
    private readonly IReportPdfCaptureService _capture;
    private readonly IReceiptPdfService _receiptPdfService;
    private readonly IDailyClosingReportService _closingReportService;
    private readonly ILogger<AdminReportPdfController> _logger;

    public AdminReportPdfController(
        AppDbContext context,
        IReportPdfStorageService storage,
        IReportPdfCaptureService capture,
        IReceiptPdfService receiptPdfService,
        IDailyClosingReportService closingReportService,
        ILogger<AdminReportPdfController> logger)
    {
        _context = context;
        _storage = storage;
        _capture = capture;
        _receiptPdfService = receiptPdfService;
        _closingReportService = closingReportService;
        _logger = logger;
    }

    /// <summary>Download a persisted RKSV closing PDF (Tagesabschluss / Monatsbeleg / Jahresbeleg).</summary>
    [HttpGet("closing/{closingId:guid}")]
    [HasPermission(AppPermissions.DailyClosingView)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadClosingPdf(
        Guid closingId,
        [FromQuery] string? language,
        CancellationToken cancellationToken)
    {
        var closing = await _context.DailyClosings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == closingId, cancellationToken)
            .ConfigureAwait(false);
        if (closing is null)
            return NotFound();

        var reportType = ReportPdfTypes.FromClosingType(closing.ClosingType);
        var normalizedLanguage = language ?? "de";
        var download = await _storage.TryOpenDownloadAsync(
            reportType,
            closingId,
            normalizedLanguage,
            cancellationToken).ConfigureAwait(false);
        if (download is not null)
            return File(download.Value.Stream, download.Value.ContentType, download.Value.FileName);

        var actorUserId = User.GetActorUserId() ?? string.Empty;
        await _capture.TryCaptureClosingReportAsync(closingId, actorUserId, normalizedLanguage, cancellationToken)
            .ConfigureAwait(false);

        download = await _storage.TryOpenDownloadAsync(
            reportType,
            closingId,
            normalizedLanguage,
            cancellationToken).ConfigureAwait(false);
        if (download is not null)
            return File(download.Value.Stream, download.Value.ContentType, download.Value.FileName);

        var pdf = await _closingReportService.TryGenerateClosingReportPdfAsync(
            closingId,
            actorUserId: null,
            normalizedLanguage,
            cancellationToken).ConfigureAwait(false);
        if (pdf is null or { Length: 0 })
            return NotFound();

        var fileName = ReportPdfStorageService.BuildDownloadFileName(reportType, closingId);
        return File(pdf, "application/pdf", fileName);
    }

    /// <summary>Download a persisted receipt / Sonderbeleg PDF (original without Nachdruck watermark).</summary>
    [HttpGet("receipt/{paymentId:guid}")]
    [HasPermission(AppPermissions.ReceiptReprint)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadReceiptPdf(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var payment = await _context.PaymentDetails.AsNoTracking()
            .Where(p => p.Id == paymentId && p.IsActive)
            .Select(p => new { p.RksvSpecialReceiptKind })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (payment is null)
            return NotFound();

        var reportType = ReportPdfTypes.FromSpecialReceiptKind(payment.RksvSpecialReceiptKind);
        var download = await _storage.TryOpenDownloadAsync(
            reportType,
            paymentId,
            "de",
            cancellationToken).ConfigureAwait(false);
        if (download is not null)
            return File(download.Value.Stream, download.Value.ContentType, download.Value.FileName);

        var actorUserId = User.GetActorUserId() ?? string.Empty;
        await _capture.TryCaptureReceiptReportAsync(
            paymentId,
            payment.RksvSpecialReceiptKind,
            actorUserId,
            cancellationToken).ConfigureAwait(false);

        download = await _storage.TryOpenDownloadAsync(
            reportType,
            paymentId,
            "de",
            cancellationToken).ConfigureAwait(false);
        if (download is not null)
            return File(download.Value.Stream, download.Value.ContentType, download.Value.FileName);

        try
        {
            var pdf = await _receiptPdfService.GeneratePdfAsync(
                paymentId,
                includeReprintWatermark: false,
                cancellationToken).ConfigureAwait(false);
            var fileName = ReportPdfStorageService.BuildDownloadFileName(reportType, paymentId);
            return File(pdf, "application/pdf", fileName);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogInformation(ex, "Stored receipt PDF not found PaymentId={PaymentId}", paymentId);
            return NotFound();
        }
    }

    /// <summary>Whether a persisted PDF exists for the given closing.</summary>
    [HttpGet("closing/{closingId:guid}/exists")]
    [HasPermission(AppPermissions.DailyClosingView)]
    [ProducesResponseType(typeof(ReportPdfExistsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportPdfExistsResponse>> ClosingPdfExists(
        Guid closingId,
        [FromQuery] string? language,
        CancellationToken cancellationToken)
    {
        var closing = await _context.DailyClosings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == closingId, cancellationToken)
            .ConfigureAwait(false);
        if (closing is null)
            return NotFound();

        var reportType = ReportPdfTypes.FromClosingType(closing.ClosingType);
        var exists = await _storage.HasStoredPdfAsync(
            reportType,
            closingId,
            language ?? "de",
            cancellationToken).ConfigureAwait(false);

        return Ok(new ReportPdfExistsResponse { HasStoredPdf = exists });
    }

    /// <summary>Whether a persisted PDF exists for the given payment/receipt.</summary>
    [HttpGet("receipt/{paymentId:guid}/exists")]
    [HasPermission(AppPermissions.ReceiptReprint)]
    [ProducesResponseType(typeof(ReportPdfExistsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportPdfExistsResponse>> ReceiptPdfExists(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var payment = await _context.PaymentDetails.AsNoTracking()
            .Where(p => p.Id == paymentId && p.IsActive)
            .Select(p => new { p.RksvSpecialReceiptKind })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (payment is null)
            return NotFound();

        var reportType = ReportPdfTypes.FromSpecialReceiptKind(payment.RksvSpecialReceiptKind);
        var exists = await _storage.HasStoredPdfAsync(reportType, paymentId, "de", cancellationToken)
            .ConfigureAwait(false);

        return Ok(new ReportPdfExistsResponse { HasStoredPdf = exists });
    }
}

public sealed class ReportPdfExistsResponse
{
    public bool HasStoredPdf { get; set; }
}
