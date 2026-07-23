using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.GracePeriods;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Deferred critical-action grace periods (schedule / cancel / list).</summary>
[Authorize]
[ApiController]
[Route("api/admin/grace-periods")]
[Produces("application/json")]
public sealed class AdminGracePeriodsController : ControllerBase
{
    private readonly IGracePeriodService _grace;
    private readonly ISettingsTenantResolver _tenantResolver;

    public AdminGracePeriodsController(
        IGracePeriodService grace,
        ISettingsTenantResolver tenantResolver)
    {
        _grace = grace;
        _tenantResolver = tenantResolver;
    }

    [HttpGet("config")]
    [ProducesResponseType(typeof(GracePeriodsConfigDto), StatusCodes.Status200OK)]
    public ActionResult<GracePeriodsConfigDto> GetConfig() => Ok(_grace.GetConfig());

    [HttpGet("active")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(IReadOnlyList<GracePeriodPendingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GracePeriodPendingDto>>> ListActive(
        CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        var rows = await _grace.ListActiveAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(rows);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(GracePeriodPendingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GracePeriodPendingDto>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        var row = await _grace.GetAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [HasPermission(AppPermissions.CashRegisterDecommission)]
    [ProducesResponseType(typeof(ScheduleGracePeriodResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScheduleGracePeriodResponse>> Schedule(
        [FromBody] ScheduleGracePeriodRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new ScheduleGracePeriodResponse
            {
                Success = false,
                ErrorCode = "INVALID_BODY",
                Message = "Request body is required.",
            });

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Only Schlussbeleg schedule is exposed via CashRegisterDecommission for now;
        // other kinds remain available for future callers with matching permissions.
        if (!string.Equals(body.ActionKind, GracePeriodActionKinds.Schlussbeleg, StringComparison.Ordinal)
            && !string.Equals(body.ActionKind, GracePeriodActionKinds.BulkDelete, StringComparison.Ordinal)
            && !string.Equals(body.ActionKind, GracePeriodActionKinds.PriceUpdate, StringComparison.Ordinal))
        {
            return BadRequest(new ScheduleGracePeriodResponse
            {
                Success = false,
                ErrorCode = "ACTION_NOT_ALLOWED",
                Message = "This action kind cannot be scheduled via this endpoint yet.",
            });
        }

        var result = await _grace.ScheduleAsync(tenantId, userId, body, cancellationToken)
            .ConfigureAwait(false);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(AppPermissions.CashRegisterDecommission)]
    [ProducesResponseType(typeof(ScheduleGracePeriodResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScheduleGracePeriodResponse>> Cancel(
        Guid id,
        [FromBody] CancelGracePeriodRequest? body,
        CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _grace
            .CancelAsync(tenantId, id, userId, body?.Reason, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success && string.Equals(result.ErrorCode, "NOT_FOUND", StringComparison.Ordinal))
            return NotFound(result);

        return result.Success ? Ok(result) : BadRequest(result);
    }
}
