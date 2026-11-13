using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>FON Ausfall / Wiederinbetriebnahme episode management (P0-3).</summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/ausfall")]
[Produces("application/json")]
public sealed class AdminTseAusfallController : ControllerBase
{
    private readonly IRksvAusfallEpisodeService _episodes;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminTseAusfallController(
        IRksvAusfallEpisodeService episodes,
        ICurrentTenantAccessor tenantAccessor)
    {
        _episodes = episodes;
        _tenantAccessor = tenantAccessor;
    }

    private bool IsSuperAdmin() => User.IsInRole(Roles.SuperAdmin);

    private Guid? ManagerScopeTenantId()
    {
        if (IsSuperAdmin())
            return null;
        return _tenantAccessor.TenantId;
    }

    private string ActorUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";

    private string ActorRole() =>
        User.IsInRole(Roles.SuperAdmin)
            ? Roles.SuperAdmin
            : User.IsInRole(Roles.Manager)
                ? Roles.Manager
                : "User";

    [HttpGet("episodes")]
    [HasPermission(AppPermissions.FinanzOnlineView)]
    [ProducesResponseType(typeof(IReadOnlyList<RksvAusfallEpisodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RksvAusfallEpisodeDto>>> ListEpisodes(
        [FromQuery] string? status = null,
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSuperAdmin() && _tenantAccessor.TenantId is null)
            return NotFound();

        var scope = IsSuperAdmin() ? tenantId : ManagerScopeTenantId();
        if (!IsSuperAdmin() && scope is null)
            return NotFound();

        var list = await _episodes.ListAsync(scope, status, cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    [HttpGet("begruendung-codes")]
    [HasPermission(AppPermissions.FinanzOnlineView)]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> ListBegruendungCodes() =>
        Ok(RksvAusfallBegruendungCodes.All);

    /// <summary>Manual Ausfall / Wiederinbetriebnahme trigger.</summary>
    [HttpPost("trigger")]
    [HasPermission(AppPermissions.FinanzOnlineSubmit)]
    [ProducesResponseType(typeof(RksvAusfallTriggerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RksvAusfallTriggerResponse>> Trigger(
        [FromBody] RksvAusfallTriggerRequest? body,
        CancellationToken cancellationToken)
    {
        if (!IsSuperAdmin() && _tenantAccessor.TenantId is null)
            return NotFound();
        if (body == null)
            return BadRequest(new RksvAusfallTriggerResponse { Success = false, ErrorCode = "AUSFALL_BODY_REQUIRED", Message = "Request body is required." });

        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var result = await _episodes
            .TriggerAsync(body, tenantId.Value, ActorUserId(), ActorRole(), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success && string.Equals(result.ErrorCode, "AUSFALL_DEVICE_NOT_FOUND", StringComparison.Ordinal))
            return NotFound(result);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("episodes/{id:guid}/approve")]
    [HasPermission(AppPermissions.FinanzOnlineSubmit)]
    [ProducesResponseType(typeof(RksvAusfallTriggerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RksvAusfallTriggerResponse>> Approve(
        Guid id,
        [FromBody] RksvAusfallApproveRequest? body,
        CancellationToken cancellationToken)
    {
        if (!IsSuperAdmin() && _tenantAccessor.TenantId is null)
            return NotFound();

        var result = await _episodes
            .ApproveAndEnqueueAsync(id, ManagerScopeTenantId(), ActorUserId(), ActorRole(), body?.OperatorNote, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success && string.Equals(result.ErrorCode, "AUSFALL_NOT_FOUND", StringComparison.Ordinal))
            return NotFound(result);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("episodes/{id:guid}/mark-manual")]
    [HasPermission(AppPermissions.FinanzOnlineSubmit)]
    [ProducesResponseType(typeof(RksvAusfallTriggerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RksvAusfallTriggerResponse>> MarkManual(
        Guid id,
        [FromBody] RksvAusfallMarkManualRequest? body,
        CancellationToken cancellationToken)
    {
        if (!IsSuperAdmin() && _tenantAccessor.TenantId is null)
            return NotFound();

        var result = await _episodes
            .MarkManualPortalAsync(id, ManagerScopeTenantId(), ActorUserId(), ActorRole(), body ?? new RksvAusfallMarkManualRequest(), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success)
            return NotFound(result);
        return Ok(result);
    }

    [HttpPost("episodes/{id:guid}/cancel")]
    [HasPermission(AppPermissions.FinanzOnlineSubmit)]
    [ProducesResponseType(typeof(RksvAusfallTriggerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RksvAusfallTriggerResponse>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!IsSuperAdmin() && _tenantAccessor.TenantId is null)
            return NotFound();

        var result = await _episodes
            .CancelSuggestionAsync(id, ManagerScopeTenantId(), ActorUserId(), ActorRole(), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success && string.Equals(result.ErrorCode, "AUSFALL_NOT_FOUND", StringComparison.Ordinal))
            return NotFound(result);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }
}
