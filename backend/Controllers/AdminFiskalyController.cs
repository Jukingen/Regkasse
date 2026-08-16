using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KasseAPI_Final.Controllers;

/// <summary>Fiskaly SIGN AT settings and FON/SCU/cash-register setup for Super Admin (enable/disable also Mandanten-Admin).</summary>
[Authorize]
[ApiController]
[Route("api/admin/fiskaly")]
[Produces("application/json")]
public sealed class AdminFiskalyController : ControllerBase
{
    private readonly IFiskalySettingsService _settings;
    private readonly IFiskalySetupService _setup;

    public AdminFiskalyController(IFiskalySettingsService settings, IFiskalySetupService setup)
    {
        _settings = settings;
        _setup = setup;
    }

    /// <summary>Effective Fiskaly settings (config + tenant/global overlay). No secrets.</summary>
    [HttpGet("settings")]
    [HasPermission(AppPermissions.CashRegisterManage)]
    [ProducesResponseType(typeof(FiskalySettingsDto), StatusCodes.Status200OK)]
    public ActionResult<FiskalySettingsDto> GetSettings() => Ok(_settings.GetSettings());

    /// <summary>Live Fiskaly status including optional authentication probe.</summary>
    [HttpGet("status")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(FiskalyStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FiskalyStatusDto>> GetStatus(
        [FromQuery] bool probeAuthentication = true,
        CancellationToken cancellationToken = default)
    {
        var status = await _settings
            .GetStatusAsync(probeAuthentication, cancellationToken)
            .ConfigureAwait(false);
        return Ok(status);
    }

    /// <summary>
    /// Persist Fiskaly Enabled overlay for the ambient tenant (Mandanten-Admin / Super Admin).
    /// Super Admin without ambient tenant writes a deployment-wide overlay. Does not write API secrets.
    /// </summary>
    [HttpPost("settings")]
    [HasPermission(AppPermissions.CashRegisterManage)]
    [ProducesResponseType(typeof(FiskalySettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FiskalySettingsDto>> UpdateSettings(
        [FromBody] UpdateFiskalySettingsRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { message = "Request body is required." });

        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";
        var updated = await _settings
            .UpdateEnabledAsync(request.Enabled, actor, cancellationToken)
            .ConfigureAwait(false);
        return Ok(updated);
    }

    /// <summary>FON / SCU / cash-register initialization snapshot (no PIN).</summary>
    [HttpGet("setup")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(FiskalySetupStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FiskalySetupStatusDto>> GetSetup(CancellationToken cancellationToken)
    {
        var status = await _setup
            .GetSetupStatusAsync(User.IsInRole(Roles.SuperAdmin), cancellationToken)
            .ConfigureAwait(false);
        return Ok(status);
    }

    /// <summary>PUT fiskaly /fon/auth. PIN is never stored or logged.</summary>
    [HttpPost("fon/authenticate")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(FiskalyFonAuthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FiskalyFonAuthDto>> AuthenticateFon(
        [FromBody] AuthenticateFonRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "Request body is required." });

        try
        {
            var result = await _setup
                .AuthenticateFonAsync(request, ActorId(), cancellationToken)
                .ConfigureAwait(false);
            return Map(result);
        }
        catch (FiskalyApiException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Create SCU if needed, then PATCH state INITIALIZED (registers SCU with FinanzOnline).</summary>
    [HttpPost("scu/initialize")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(FiskalyScuSetupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FiskalyScuSetupDto>> InitializeScu(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _setup
                .InitializeScuAsync(ActorId(), cancellationToken)
                .ConfigureAwait(false);
            return Map(result);
        }
        catch (FiskalyApiException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Create cash register if needed, PATCH REGISTERED then INITIALIZED.
    /// SIGN AT cash_register_id is the local cash register UUID.
    /// </summary>
    [HttpPost("cash-register/{id:guid}/initialize")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(FiskalyCashRegisterSetupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FiskalyCashRegisterSetupDto>> InitializeCashRegister(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _setup
                .InitializeCashRegisterAsync(id, ActorId(), User.IsInRole(Roles.SuperAdmin), cancellationToken)
                .ConfigureAwait(false);
            return Map(result);
        }
        catch (FiskalyApiException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private string ActorId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";

    private ActionResult<T> Map<T>(FiskalySetupOperationResult<T> result)
    {
        if (result.Success && result.Data is not null)
            return Ok(result.Data);

        if (result.StatusCode == StatusCodes.Status404NotFound)
            return NotFound(new { message = result.Message });

        return BadRequest(new { message = result.Message });
    }
}
