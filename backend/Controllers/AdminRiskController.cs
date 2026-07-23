using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.RiskScoring;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin risk scoring / anomaly inbox for tenant user actions.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/risk")]
[Produces("application/json")]
public sealed class AdminRiskController : ControllerBase
{
    private readonly IRiskScoringService _riskScoring;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminRiskController(
        IRiskScoringService riskScoring,
        ICurrentTenantAccessor tenantAccessor)
    {
        _riskScoring = riskScoring;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>List risk scores across tenants (Super Admin).</summary>
    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(RiskScoreListResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskScoreListResponseDto>> List(
        [FromQuery] bool unresolvedOnly = true,
        [FromQuery] string? riskLevel = null,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var result = await _riskScoring
            .ListAsync(unresolvedOnly, riskLevel, limit, offset, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Mark a risk score as resolved with a resolution note.</summary>
    [HttpPost("{id:guid}/resolve")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(RiskScoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RiskScoreDto>> Resolve(
        Guid id,
        [FromBody] ResolveRiskScoreRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Resolution))
            return BadRequest(new { code = "RESOLUTION_REQUIRED", message = "Resolution note is required." });

        var actorId = User.GetActorUserId();
        if (string.IsNullOrEmpty(actorId))
            return Unauthorized();

        try
        {
            var updated = await _riskScoring
                .ResolveAsync(id, actorId, body.Resolution, cancellationToken)
                .ConfigureAwait(false);

            if (updated is null)
                return NotFound(new { code = "RISK_NOT_FOUND", message = "Risk score not found." });

            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_RESOLUTION", message = ex.Message });
        }
    }

    /// <summary>
    /// Evaluate (and optionally persist) risk for the current actor in tenant context.
    /// Intended for integration tests / guarded internal callers.
    /// </summary>
    [HttpPost("evaluate")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(EvaluateUserActionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EvaluateUserActionResponseDto>> Evaluate(
        [FromBody] EvaluateUserActionRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.ActionType))
            return BadRequest(new { code = "INVALID_BODY", message = "ActionType is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return BadRequest(new { code = "TENANT_CONTEXT_REQUIRED", message = "Tenant context is required." });

        var action = new UserAction
        {
            TenantId = tenantId.Value,
            UserId = userId,
            ActionType = body.ActionType.Trim(),
            Timestamp = body.Timestamp ?? DateTime.UtcNow,
            BulkCount = body.BulkCount,
            AverageBulkCount = body.AverageBulkCount,
            IsKnownIp = body.IsKnownIp,
            IsRapidSuccession = body.IsRapidSuccession,
            IsFirstTime = body.IsFirstTime,
            IpAddress = body.IpAddress,
        };

        var result = await _riskScoring
            .EvaluateAsync(action, body.PersistIfElevated, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }
}
