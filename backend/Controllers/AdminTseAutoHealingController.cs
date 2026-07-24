using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE auto-healing (re-probe / clear errors / optional failover — not fiscal rewrite).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/auto-healing")]
[Produces("application/json")]
public sealed class AdminTseAutoHealingController : ControllerBase
{
    private readonly ITseAutoHealingService _healing;

    public AdminTseAutoHealingController(ITseAutoHealingService healing)
    {
        _healing = healing;
    }

    [HttpGet("configuration")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseHealingConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseHealingConfigurationDto>> GetConfiguration(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _healing.GetHealingConfigurationAsync(tenantId, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("configuration")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseHealingConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseHealingConfigurationDto>> Configure(
        [FromQuery] Guid tenantId,
        [FromBody] ConfigureTseHealingRequestDto? body,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        body ??= new ConfigureTseHealingRequestDto();
        try
        {
            return Ok(await _healing
                .ConfigureHealingAsync(tenantId, body, User.GetActorUserId(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("diagnose/{deviceId:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseHealingResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseHealingResultDto>> DiagnoseAndHeal(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _healing
                .DiagnoseAndHealAsync(deviceId, User.GetActorUserId(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("history")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseHealingReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseHealingReportDto>> GetHistory(
        [FromQuery] Guid tenantId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _healing.GetHealingHistoryAsync(tenantId, take, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
