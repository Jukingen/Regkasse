using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Operations;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Reversible admin operation journal (list / detail / undo).</summary>
[Authorize]
[ApiController]
[Route("api/admin/operation-logs")]
[Produces("application/json")]
[HasPermission(AppPermissions.AuditView)]
public sealed class AdminOperationLogsController : ControllerBase
{
    private readonly IOperationLogService _logs;
    private readonly IOperationUndoService _undo;
    private readonly ISettingsTenantResolver _tenantResolver;

    public AdminOperationLogsController(
        IOperationLogService logs,
        IOperationUndoService undo,
        ISettingsTenantResolver tenantResolver)
    {
        _logs = logs;
        _undo = undo;
        _tenantResolver = tenantResolver;
    }

    [HttpGet]
    [ProducesResponseType(typeof(OperationLogListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationLogListResponseDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? operationType = null,
        [FromQuery] bool? isUndone = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        var result = await _logs
            .ListAsync(tenantId, page, pageSize, operationType, isUndone, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OperationLogDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationLogDetailDto>> Get(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        var row = await _logs.GetAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("{id:guid}/undo")]
    [ProducesResponseType(typeof(UndoOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UndoOperationResponse>> Undo(
        Guid id,
        [FromBody] UndoOperationRequest? body,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _undo
            .UndoOperationAsync(tenantId, id, userId, body?.Reason, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            if (string.Equals(result.ErrorCode, "NOT_FOUND", StringComparison.Ordinal)
                || string.Equals(result.ErrorCode, "ENTITY_NOT_FOUND", StringComparison.Ordinal))
                return NotFound(result);

            return BadRequest(result);
        }

        return Ok(result);
    }
}
