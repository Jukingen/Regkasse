using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
    private readonly ILogger<PosCashRegisterController> _logger;

    public PosCashRegisterController(
        IPosCashRegisterReadinessService readiness,
        ICashRegisterResolutionService cashRegisterResolution,
        IPosCriticalActionAuditService posCriticalAudit,
        ILogger<PosCashRegisterController> logger)
    {
        _readiness = readiness;
        _cashRegisterResolution = cashRegisterResolution;
        _posCriticalAudit = posCriticalAudit;
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
    /// Response shape (camelCase): <c>{ "registers": [ { "id", "registerNumber", "location" } ], "emptyReason": ... }</c>.
    /// Do not substitute <c>GET /api/CashRegister</c> on POS — that returns full inventory including closed registers.
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
}
