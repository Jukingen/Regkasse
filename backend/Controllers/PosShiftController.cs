using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// POS cashier shift lifecycle (start/end, current shift, closing summary). Fiscal Tagesabschluss remains on
/// <see cref="TagesabschlussController"/>; register occupancy is synchronized via <see cref="ICashRegisterShiftService"/>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/pos/shift")]
public sealed class PosShiftController : ControllerBase
{
    private readonly IPosShiftService _shiftService;
    private readonly IPosDailyClosingService _dailyClosing;
    private readonly IDailyClosingReportService _dailyClosingReport;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<PosShiftController> _logger;

    public PosShiftController(
        IPosShiftService shiftService,
        IPosDailyClosingService dailyClosing,
        IDailyClosingReportService dailyClosingReport,
        IAuditLogService auditLog,
        ILogger<PosShiftController> logger)
    {
        _shiftService = shiftService;
        _dailyClosing = dailyClosing;
        _dailyClosingReport = dailyClosingReport;
        _auditLog = auditLog;
        _logger = logger;
    }

    /// <summary>Returns the caller's active shift, if any.</summary>
    [HttpGet("current")]
    [HasPermission(AppPermissions.ShiftView)]
    [ProducesResponseType(typeof(CurrentShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentShiftResponse>> GetCurrentShift(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var response = await _shiftService.GetCurrentShiftAsync(userId, cancellationToken);
        return Ok(response);
    }

    /// <summary>Starts a cashier shift and opens the target register (idempotent when already open by same user).</summary>
    [HttpPost("start")]
    [HasPermission(AppPermissions.ShiftOpen)]
    [ProducesResponseType(typeof(CashierShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashierShiftDto>> StartShift(
        [FromBody] StartShiftRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required" });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var actorRole = User.GetActorRole() ?? Roles.FallbackUnknown;

        try
        {
            var shift = await _shiftService.StartShiftAsync(
                userId,
                User.Identity?.Name ?? string.Empty,
                request,
                cancellationToken);

            await _auditLog.LogSystemOperationAsync(
                "ShiftStarted",
                "cashier_shift",
                userId,
                actorRole,
                description: $"Shift started with balance {request.StartBalance:F2}",
                actionType: AuditEventType.Other,
                entityId: shift.Id,
                tenantId: shift.TenantId);

            return Ok(shift);
        }
        catch (PosShiftStartException ex) when (ex.Kind == PosShiftStartResultKind.AlreadyActive)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (PosShiftStartException ex) when (ex.Kind == PosShiftStartResultKind.RegisterNotFound)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (PosShiftStartException ex) when (ex.Kind == PosShiftStartResultKind.RegisterOpenConflict)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (PosShiftStartException ex)
        {
            _logger.LogWarning(ex, "StartShift failed for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Auto-opens a cashier shift for the caller's resolved register (idempotent if already active).
    /// Does not require a start balance; uses the register current balance.
    /// </summary>
    [HttpPost("auto-open")]
    [HasPermission(AppPermissions.ShiftOpen)]
    [ProducesResponseType(typeof(CashierShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashierShiftDto>> AutoOpenShift(
        [FromBody] AutoOpenShiftRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required" });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        try
        {
            var shift = await _shiftService.AutoOpenShiftAsync(
                userId,
                User.Identity?.Name ?? string.Empty,
                request.CashRegisterId,
                cancellationToken);
            return Ok(shift);
        }
        catch (PosShiftStartException ex) when (ex.Kind == PosShiftStartResultKind.RegisterNotFound)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (PosShiftStartException ex) when (ex.Kind == PosShiftStartResultKind.RegisterOpenConflict)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (PosShiftStartException ex)
        {
            _logger.LogWarning(ex, "AutoOpenShift failed for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Soft-closes the caller's active CashierShift without closing the cash register (idempotent).
    /// </summary>
    [HttpPost("auto-close")]
    [HasPermission(AppPermissions.ShiftClose)]
    [ProducesResponseType(typeof(CashierShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AutoCloseShift(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var actorRole = User.GetActorRole() ?? Roles.FallbackUnknown;
        var shift = await _shiftService.AutoCloseShiftAsync(userId, actorRole, cancellationToken);
        if (shift == null)
            return NoContent();

        return Ok(shift);
    }

    /// <summary>Ends the active shift, closes the register, and returns a non-fiscal closing summary.</summary>
    [HttpPost("end")]
    [HasPermission(AppPermissions.ShiftClose)]
    [ProducesResponseType(typeof(EndShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EndShiftResponse>> EndShift(
        [FromBody] EndShiftRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required" });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var actorRole = User.GetActorRole() ?? Roles.FallbackUnknown;

        try
        {
            var result = await _shiftService.EndShiftAsync(userId, actorRole, request, cancellationToken);
            return Ok(result);
        }
        catch (PosShiftEndException ex) when (ex.Kind == PosShiftEndResultKind.NoActiveShift)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (PosShiftEndException ex) when (ex.Kind == PosShiftEndResultKind.RegisterCloseForbidden)
        {
            return Forbid();
        }
        catch (PosShiftEndException ex)
        {
            _logger.LogWarning(ex, "EndShift failed for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Readiness for fiscal Tagesabschluss on the active shift register.</summary>
    [HttpGet("daily-closing/status")]
    [HasPermission(AppPermissions.TseSign)]
    [ProducesResponseType(typeof(PosDailyClosingStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PosDailyClosingStatusDto>> GetDailyClosingStatus(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var status = await _dailyClosing.GetStatusAsync(userId, cancellationToken);
        return Ok(status);
    }

    /// <summary>
    /// Fiscal daily closing (Tagesabschluss) for the active shift register; records cash count on the shift row.
    /// </summary>
    [HttpPost("daily-closing")]
    [HasPermission(AppPermissions.TseSign)]
    [ProducesResponseType(typeof(PosDailyClosingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PosDailyClosingResult>> DailyClosing(
        [FromBody] PosDailyClosingRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required" });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var actorRole = User.GetActorRole() ?? Roles.FallbackUnknown;

        try
        {
            var result = await _dailyClosing.PerformDailyClosingAsync(userId, actorRole, request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new
                {
                    error = result.ErrorMessage,
                    paymentsWithoutInvoiceCount = result.PaymentsWithoutInvoiceCount,
                });
            }

            return Ok(result);
        }
        catch (PosDailyClosingException ex) when (ex.Kind == PosDailyClosingFailureKind.NoActiveShift)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (PosDailyClosingException ex) when (ex.Kind == PosDailyClosingFailureKind.AlreadyClosed)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (PosDailyClosingException ex) when (ex.Kind == PosDailyClosingFailureKind.FiscalBlocked)
        {
            return BadRequest(new
            {
                error = ex.Message,
                paymentsWithoutInvoiceCount = ex.PaymentsWithoutInvoiceCount,
            });
        }
        catch (PosDailyClosingException ex) when (ex.Kind == PosDailyClosingFailureKind.RegisterCloseForbidden)
        {
            return Forbid();
        }
        catch (PosDailyClosingException ex) when (ex.Kind == PosDailyClosingFailureKind.RegisterCloseFailed)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (PosDailyClosingException ex)
        {
            _logger.LogWarning(ex, "DailyClosing failed for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Localized PDF report for a completed daily closing linked to the caller's shift.</summary>
    [HttpGet("daily-closing/{dailyClosingId:guid}/report.pdf")]
    [HasPermission(AppPermissions.TseSign)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDailyClosingReportPdf(
        Guid dailyClosingId,
        [FromQuery] string? language,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var pdf = await _dailyClosingReport.TryGenerateClosingReportPdfAsync(
            dailyClosingId,
            userId,
            language ?? "de",
            cancellationToken);

        if (pdf == null || pdf.Length == 0)
            return NotFound(new { error = "Daily closing report not found" });

        var fileName = $"Tagesabschluss_{dailyClosingId:N}.pdf";
        return File(pdf, "application/pdf", fileName);
    }
}
