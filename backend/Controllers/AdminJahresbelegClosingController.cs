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

/// <summary>RKSV Phase 3 Jahresbeleg (yearly closing with Monatsbeleg aggregation).</summary>
[Authorize]
[ApiController]
[Route("api/admin/jahresbeleg-closing")]
[Produces("application/json")]
public class AdminJahresbelegClosingController : ControllerBase
{
    private readonly IJahresbelegClosingService _jahresbelegClosing;
    private readonly IDailyClosingReportService _reportService;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly ILogger<AdminJahresbelegClosingController> _logger;

    public AdminJahresbelegClosingController(
        IJahresbelegClosingService jahresbelegClosing,
        IDailyClosingReportService reportService,
        ISettingsTenantResolver tenantResolver,
        ILogger<AdminJahresbelegClosingController> logger)
    {
        _jahresbelegClosing = jahresbelegClosing;
        _reportService = reportService;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    /// <summary>Preview yearly totals from Monatsbeleg rows without TSE signing.</summary>
    [HttpGet("preview")]
    [HasPermission(AppPermissions.DailyClosingView)]
    [ProducesResponseType(typeof(JahresbelegSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JahresbelegSummaryDto>> Preview(
        [FromQuery] Guid cashRegisterId,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        if (cashRegisterId == Guid.Empty)
            return BadRequest(new { message = "cashRegisterId is required" });

        var y = year ?? PostgreSqlUtcDateTime.GetViennaCurrentYearMonth().Year;
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var summary = await _jahresbelegClosing.GenerateYearlySummaryPreviewAsync(
            tenantId,
            cashRegisterId,
            y,
            cancellationToken);
        return Ok(summary);
    }

    /// <summary>Create RKSV Jahresbeleg for a Vienna calendar year (default: current year).</summary>
    [HttpPost]
    [HasPermission(AppPermissions.RksvJahresbelegCreate)]
    [ProducesResponseType(typeof(JahresbelegClosingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JahresbelegClosingResult>> Create(
        [FromBody] CreateJahresbelegClosingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _jahresbelegClosing.CreateJahresbelegClosingAsync(userId, request, cancellationToken);
        if (!result.Success)
        {
            _logger.LogWarning("Jahresbeleg closing blocked: {Reason}", result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result);
    }

    [HttpGet]
    [HasPermission(AppPermissions.DailyClosingView)]
    [ProducesResponseType(typeof(IReadOnlyList<JahresbelegListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JahresbelegListItemDto>>> List(
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var items = await _jahresbelegClosing.ListAsync(cashRegisterId, year, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.DailyClosingView)]
    [ProducesResponseType(typeof(JahresbelegDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JahresbelegDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _jahresbelegClosing.GetByIdAsync(id, cancellationToken);
        return detail == null ? NotFound() : Ok(detail);
    }

    /// <summary>Localized PDF for a persisted Jahresbeleg.</summary>
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
        var report = await _jahresbelegClosing.BuildReportDtoAsync(id, cancellationToken);
        if (report == null)
            return NotFound();

        var pdf = _reportService.GenerateDailyReportPdf(report, language ?? "de");
        return File(pdf, "application/pdf", $"jahresbeleg-{report.BusinessDate:yyyy}.pdf");
    }
}
