using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>RKSV Phase 2 Monatsbeleg (monthly closing with daily-closing aggregation).</summary>
[Authorize]
[ApiController]
[Route("api/admin/monatsbeleg-closing")]
[Produces("application/json")]
public class AdminMonatsbelegClosingController : ControllerBase
{
    private readonly IMonatsbelegClosingService _monatsbelegClosing;
    private readonly IDailyClosingReportService _reportService;
    private readonly IReportPdfStorageService _reportPdfStorage;
    private readonly IReportPdfCaptureService _reportPdfCapture;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly ILogger<AdminMonatsbelegClosingController> _logger;

    public AdminMonatsbelegClosingController(
        IMonatsbelegClosingService monatsbelegClosing,
        IDailyClosingReportService reportService,
        IReportPdfStorageService reportPdfStorage,
        IReportPdfCaptureService reportPdfCapture,
        ISettingsTenantResolver tenantResolver,
        ILogger<AdminMonatsbelegClosingController> logger)
    {
        _monatsbelegClosing = monatsbelegClosing;
        _reportService = reportService;
        _reportPdfStorage = reportPdfStorage;
        _reportPdfCapture = reportPdfCapture;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    /// <summary>Preview monthly totals from daily closings without TSE signing.</summary>
    [HttpGet("preview")]
    [HasPermission(AppPermissions.DailyClosingView)]
    [ProducesResponseType(typeof(MonatsbelegSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonatsbelegSummaryDto>> Preview(
        [FromQuery] Guid cashRegisterId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        if (cashRegisterId == Guid.Empty)
            return BadRequest(new { message = "cashRegisterId is required" });

        var (y, m) = ResolvePeriod(year, month);
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var summary = await _monatsbelegClosing.GenerateMonthlySummaryPreviewAsync(
            tenantId,
            cashRegisterId,
            y,
            m,
            cancellationToken);
        return Ok(summary);
    }

    /// <summary>Create RKSV Monatsbeleg for a Vienna calendar month (default: current month).</summary>
    [HttpPost]
    [HasPermission(AppPermissions.DailyClosingExecute)]
    [ProducesResponseType(typeof(MonatsbelegClosingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MonatsbelegClosingResult>> Create(
        [FromBody] CreateMonatsbelegClosingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _monatsbelegClosing.CreateMonatsbelegClosingAsync(userId, request, cancellationToken);
        if (!result.Success)
        {
            _logger.LogWarning("Monatsbeleg closing blocked: {Reason}", result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result);
    }

    [HttpGet]
    [HasPermission(AppPermissions.DailyClosingView)]
    [ProducesResponseType(typeof(IReadOnlyList<MonatsbelegListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MonatsbelegListItemDto>>> List(
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        var items = await _monatsbelegClosing.ListAsync(cashRegisterId, year, month, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.DailyClosingView)]
    [ProducesResponseType(typeof(MonatsbelegDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonatsbelegDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _monatsbelegClosing.GetByIdAsync(id, cancellationToken);
        return detail == null ? NotFound() : Ok(detail);
    }

    /// <summary>Localized PDF for a persisted Monatsbeleg.</summary>
    [HttpGet("{id:guid}/report-pdf")]
    [HasPermission(AppPermissions.DailyClosingView)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadReportPdf(
        Guid id,
        [FromQuery] string? language,
        CancellationToken cancellationToken)
    {
        var detail = await _monatsbelegClosing.GetByIdAsync(id, cancellationToken);
        if (detail == null)
            return NotFound();

        var normalizedLanguage = language ?? "de";
        if (detail.DailyClosingId is Guid closingId)
        {
            var stored = await TryOpenStoredClosingPdfAsync(
                ReportPdfTypes.Monatsbeleg,
                closingId,
                normalizedLanguage,
                cancellationToken).ConfigureAwait(false);
            if (stored is not null)
                return stored;
        }

        var report = await _monatsbelegClosing.BuildReportDtoAsync(id, cancellationToken);
        if (report == null)
            return NotFound();

        var pdf = _reportService.GenerateDailyReportPdf(report, normalizedLanguage);
        return File(pdf, "application/pdf", $"monatsbeleg-{report.BusinessDate:yyyy-MM}.pdf");
    }

    private async Task<IActionResult?> TryOpenStoredClosingPdfAsync(
        string reportType,
        Guid closingId,
        string language,
        CancellationToken cancellationToken)
    {
        var download = await _reportPdfStorage.TryOpenDownloadAsync(
            reportType,
            closingId,
            language,
            cancellationToken).ConfigureAwait(false);
        if (download is not null)
            return File(download.Value.Stream, download.Value.ContentType, download.Value.FileName);

        var actorUserId = User.GetActorUserId() ?? string.Empty;
        await _reportPdfCapture.TryCaptureClosingReportAsync(
            closingId,
            actorUserId,
            language,
            cancellationToken).ConfigureAwait(false);

        download = await _reportPdfStorage.TryOpenDownloadAsync(
            reportType,
            closingId,
            language,
            cancellationToken).ConfigureAwait(false);
        if (download is not null)
            return File(download.Value.Stream, download.Value.ContentType, download.Value.FileName);

        var pdf = await _reportService.TryGenerateClosingReportPdfAsync(
            closingId,
            actorUserId: null,
            language,
            cancellationToken).ConfigureAwait(false);
        if (pdf is not { Length: > 0 })
            return null;

        var fileName = ReportPdfStorageService.BuildDownloadFileName(reportType, closingId);
        return File(pdf, "application/pdf", fileName);
    }

    private static (int Year, int Month) ResolvePeriod(int? year, int? month)
    {
        var (currentYear, currentMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        return (year ?? currentYear, month ?? currentMonth);
    }
}
