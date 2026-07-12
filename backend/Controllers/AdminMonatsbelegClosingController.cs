using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
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
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly ILogger<AdminMonatsbelegClosingController> _logger;

    public AdminMonatsbelegClosingController(
        IMonatsbelegClosingService monatsbelegClosing,
        IDailyClosingReportService reportService,
        ISettingsTenantResolver tenantResolver,
        ILogger<AdminMonatsbelegClosingController> logger)
    {
        _monatsbelegClosing = monatsbelegClosing;
        _reportService = reportService;
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
        var report = await _monatsbelegClosing.BuildReportDtoAsync(id, cancellationToken);
        if (report == null)
            return NotFound();

        var pdf = _reportService.GenerateDailyReportPdf(report, language ?? "de");
        return File(pdf, "application/pdf", $"monatsbeleg-{report.BusinessDate:yyyy-MM}.pdf");
    }

    private static (int Year, int Month) ResolvePeriod(int? year, int? month)
    {
        var (currentYear, currentMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        return (year ?? currentYear, month ?? currentMonth);
    }
}
