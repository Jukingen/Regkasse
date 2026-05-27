using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin manual restore with second-admin approval. Validation-only isolated database restore; never production.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/restore")]
[Produces("application/json")]
public sealed class AdminRestoreController : ControllerBase
{
    private readonly IManualRestoreTriggerService _service;

    public AdminRestoreController(IManualRestoreTriggerService service) => _service = service;

    /// <summary>Request a validation-only restore (pending second Super Admin approval).</summary>
    [HttpPost("request")]
    [ProducesResponseType(typeof(RestoreRequestStatus), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RestoreRequestStatus>> CreateRequest(
        [FromBody] RestoreRequest body,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        try
        {
            var status = await _service.CreateRequestAsync(
                body,
                User.GetActorUserId() ?? "unknown",
                User.GetActorEmail(),
                correlationId,
                cancellationToken);
            return CreatedAtAction(nameof(GetRequest), new { requestId = status.RequestId }, status);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { code = "MANUAL_RESTORE_DISABLED", error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "MANUAL_RESTORE_VALIDATION", error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "BACKUP_RUN_NOT_FOUND", error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { code = "MANUAL_RESTORE_CONFLICT", error = ex.Message });
        }
    }

    /// <summary>Approve or reject a pending request (different Super Admin; token from email).</summary>
    [HttpPost("approve/{requestId:guid}")]
    [ProducesResponseType(typeof(RestoreRequestStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RestoreRequestStatus>> ProcessApproval(
        Guid requestId,
        [FromBody] RestoreApprovalRequest body,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        try
        {
            var status = await _service.ProcessApprovalAsync(
                requestId,
                body,
                User.GetActorUserId() ?? "unknown",
                User.GetActorEmail(),
                correlationId,
                cancellationToken);
            return Ok(status);
        }
        catch (ManualRestoreApprovalException ex)
        {
            return BadRequest(new { code = ex.Code, error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "MANUAL_RESTORE_VALIDATION", error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { code = "MANUAL_RESTORE_APPROVAL_CONFLICT", error = ex.Message });
        }
    }

    /// <summary>Get restore request status.</summary>
    [HttpGet("request/{requestId:guid}")]
    [ProducesResponseType(typeof(RestoreRequestStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestoreRequestStatus>> GetRequest(Guid requestId, CancellationToken cancellationToken)
    {
        var status = await _service.GetStatusAsync(requestId, cancellationToken);
        return status == null ? NotFound() : Ok(status);
    }

    /// <summary>Paginated restore request history (newest first).</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(RestoreRequestHistoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RestoreRequestHistoryResponse>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var history = await _service.GetHistoryAsync(page, pageSize, cancellationToken);
        return Ok(history);
    }
}
