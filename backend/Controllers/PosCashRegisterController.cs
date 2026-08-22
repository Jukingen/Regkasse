using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// POS session cash-register readiness (nextAction, effective register, optional auto-open). Not called by payment creation;
/// payment authorizes <c>CashRegisterId</c> via <see cref="ICashRegisterResolutionService.ValidatePaymentRegisterAsync"/> and re-validates at DB commit via <see cref="ICashRegisterResolutionService.ValidatePaymentRegisterForCommitAsync"/>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/pos/cash-register")]
public sealed class PosCashRegisterController : ControllerBase
{
    private readonly IPosCashRegisterReadinessService _readiness;
    private readonly ICashRegisterResolutionService _cashRegisterResolution;
    private readonly IPosCriticalActionAuditService _posCriticalAudit;
    private readonly AppDbContext _db;
    private readonly ILogger<PosCashRegisterController> _logger;

    public PosCashRegisterController(
        IPosCashRegisterReadinessService readiness,
        ICashRegisterResolutionService cashRegisterResolution,
        IPosCriticalActionAuditService posCriticalAudit,
        AppDbContext db,
        ILogger<PosCashRegisterController> logger)
    {
        _readiness = readiness;
        _cashRegisterResolution = cashRegisterResolution;
        _posCriticalAudit = posCriticalAudit;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Read-only current POS register context (no auto-open / no settings mutation).
    /// Prefer this for diagnostics; session bootstrap still uses <see cref="EnsureReady"/>.
    /// </summary>
    [HttpGet("current")]
    [HasPermission(AppPermissions.CartView)]
    [ProducesResponseType(typeof(PosCashRegisterContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PosCashRegisterContextDto>> GetCurrent(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("GetCurrent: no user id in claims");
            return Unauthorized(new { message = "User not authenticated" });
        }

        var dto = await _readiness.GetReadinessSnapshotForPosAsync(userId, User, cancellationToken);
        return Ok(dto);
    }

    /// <summary>
    /// Returns session DTO for the POS client; may auto-open when feature flags allow. Does not gate <c>POST /api/pos/payment</c> by itself.
    /// </summary>
    [HttpPost("ensure-ready")]
    [HasPermission(AppPermissions.CartView)]
    [ProducesResponseType(typeof(PosCashRegisterContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PosCashRegisterContextDto>> EnsureReady(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("EnsureReady: no user id in claims");
            return Unauthorized(new { message = "User not authenticated" });
        }

        var dto = await _readiness.EnsureReadyForPosAsync(userId, User, cancellationToken);
        await _posCriticalAudit.LogEnsureReadyOutcomeAsync(userId, dto, cancellationToken);
        return Ok(dto);
    }

    /// <summary>
    /// Canonical POS selectable-register list: <see cref="ICashRegisterResolutionService.ListSelectableForPosPickerAsync"/> (wraps
    /// <see cref="ICashRegisterResolutionService.ListSelectableRegistersAsync"/> and adds <c>emptyReason</c> when the list is empty).
    /// </summary>
    /// <remarks>
    /// Response shape (camelCase): <c>{ "registers": [ { "id", "registerNumber", "location", "status" } ], "emptyReason": ... }</c>,
    /// where <c>status</c> is the <see cref="RegisterStatus"/> name (<c>"Open"</c> / <c>"Closed"</c>) — closed rows are selectable and
    /// get opened by <c>POST /api/pos/shift/auto-open</c> on pick.
    /// Do not substitute <c>GET /api/CashRegister</c> on POS — that returns full inventory including decommissioned registers.
    /// </remarks>
    [HttpGet("selectable")]
    [HasPermission(AppPermissions.CartView)]
    [ProducesResponseType(typeof(PosSelectableListResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> ListSelectable(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("ListSelectable: no user id in claims");
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _cashRegisterResolution.ListSelectableForPosPickerAsync(userId, User, cancellationToken);
        return Ok(new { registers = result.Registers, emptyReason = result.EmptyReason });
    }

    /// <summary>
    /// Persists the caller's default POS cash register on <c>UserSettings.CashRegisterId</c>
    /// (same preference used by shift auto-open when the request omits a register id).
    /// </summary>
    [HttpPost("default")]
    [HasPermission(AppPermissions.CartView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ShiftAutoOpenResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetDefaultRegister(
        [FromBody] SetDefaultCashRegisterRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        if (request == null || request.RegisterId == Guid.Empty)
        {
            return BadRequest(ShiftAutoOpenResult.Fail(
                ShiftAutoOpenCodes.NeedRegisterSelection,
                ShiftAutoOpenMessages.NeedRegisterSelection));
        }

        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RegisterId, cancellationToken);
        if (register == null)
        {
            return BadRequest(ShiftAutoOpenResult.Fail(
                ShiftAutoOpenCodes.RegisterNotFound,
                ShiftAutoOpenMessages.RegisterNotFound));
        }

        if (register.Status == RegisterStatus.Decommissioned)
        {
            return BadRequest(ShiftAutoOpenResult.Fail(
                ShiftAutoOpenCodes.RegisterDecommissioned,
                ShiftAutoOpenMessages.RegisterDecommissioned));
        }

        var assignCheck = await _cashRegisterResolution.ValidateAssignmentChangeAsync(
            userId,
            request.RegisterId.ToString(),
            User,
            cancellationToken);
        if (!assignCheck.Ok)
        {
            var (code, message) = assignCheck.Code switch
            {
                CashRegisterResolutionCodes.Decommissioned => (
                    ShiftAutoOpenCodes.RegisterDecommissioned,
                    ShiftAutoOpenMessages.RegisterDecommissioned),
                CashRegisterResolutionCodes.NotFound => (
                    ShiftAutoOpenCodes.RegisterNotFound,
                    ShiftAutoOpenMessages.RegisterNotFound),
                _ => (
                    ShiftAutoOpenCodes.RegisterUnavailable,
                    ShiftAutoOpenMessages.RegisterUnavailable),
            };
            return BadRequest(ShiftAutoOpenResult.Fail(code, message));
        }

        var settings = await UserSettingsBootstrap.GetOrCreateTrackedUserSettingsAsync(
            _db,
            userId,
            cancellationToken);
        settings.CashRegisterId = request.RegisterId.ToString("D");
        settings.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Persisted default cash register {RegisterId} for user {UserId}",
            request.RegisterId,
            userId);

        return Ok(new { success = true, cashRegisterId = settings.CashRegisterId });
    }

    /// <summary>
    /// Returns the caller's persisted default POS cash register id (<c>UserSettings.CashRegisterId</c>).
    /// </summary>
    [HttpGet("default")]
    [HasPermission(AppPermissions.CartView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDefaultRegister(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var raw = await _db.UserSettings.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.CashRegisterId)
            .FirstOrDefaultAsync(cancellationToken);

        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed) ||
            !Guid.TryParse(trimmed, out var id) ||
            id == Guid.Empty)
        {
            return Ok(new { registerId = (string?)null });
        }

        return Ok(new { registerId = id.ToString("D") });
    }
}
