using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE auto-scaling (recommendation-first; soft backup stubs only when AutoProvision + Development).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/auto-scaling")]
[Produces("application/json")]
public sealed class AdminTseAutoScalingController : ControllerBase
{
    private readonly ITseAutoScalingService _scaling;

    public AdminTseAutoScalingController(ITseAutoScalingService scaling)
    {
        _scaling = scaling;
    }

    [HttpGet("status")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseScalingStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseScalingStatusDto>> GetStatus(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _scaling.GetScalingStatusAsync(tenantId, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("policy")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseScalingPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseScalingPolicyDto>> GetPolicy(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _scaling.GetScalingPolicyAsync(tenantId, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("policy")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseScalingPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseScalingPolicyDto>> ConfigurePolicy(
        [FromQuery] Guid tenantId,
        [FromBody] ConfigureTseScalingPolicyRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        body ??= new ConfigureTseScalingPolicyRequestDto();
        try
        {
            return Ok(await _scaling
                .ConfigureScalingPolicyAsync(tenantId, body, User.GetActorUserId(), cancellationToken)
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

    [HttpPost("evaluate")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseScalingResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseScalingResultDto>> EvaluateAndScale(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _scaling
                .EvaluateAndScaleAsync(tenantId, User.GetActorUserId(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("history")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseScalingHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseScalingHistoryDto>> GetHistory(
        [FromQuery] Guid tenantId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _scaling
                .GetScalingHistoryAsync(tenantId, take, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
