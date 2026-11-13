using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.CriticalActions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Critical-action approval requests (request / Super Admin inbox / claim).</summary>
[Authorize]
[ApiController]
[Route("api/admin/approvals")]
[Produces("application/json")]
public sealed class AdminApprovalsController : ControllerBase
{
    private readonly IApprovalService _approvals;

    public AdminApprovalsController(IApprovalService approvals)
    {
        _approvals = approvals;
    }

    [HttpGet("pending")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(IReadOnlyList<ApprovalRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApprovalRequestDto>>> ListPending(
        CancellationToken cancellationToken)
    {
        var rows = await _approvals.ListPendingAsync(cancellationToken).ConfigureAwait(false);
        return Ok(rows);
    }

    /// <summary>Historical approval requests (all statuses) for reporting / audit UI.</summary>
    [HttpGet("history")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(IReadOnlyList<ApprovalRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApprovalRequestDto>>> ListHistory(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? actionType = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var rows = await _approvals
            .ListHistoryAsync(
                new ApprovalHistoryQuery
                {
                    TenantId = tenantId,
                    Status = status,
                    ActionType = actionType,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    Limit = limit,
                    Offset = offset,
                },
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(rows);
    }

    /// <summary>Aggregate approval history report (default last 30 days).</summary>
    [HttpGet("history/report")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApprovalHistoryReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalHistoryReportDto>> GetHistoryReport(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var report = await _approvals
            .GetHistoryReportAsync(fromUtc, toUtc, tenantId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(report);
    }

    [HttpGet("{requestId:guid}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApprovalRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApprovalRequestDto>> Get(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var row = await _approvals.GetAsync(requestId, cancellationToken).ConfigureAwait(false);
        return row is null ? NotFound() : Ok(row);
    }

    /// <summary>Any authenticated admin user may request Super Admin approval for a critical action.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApprovalMutationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApprovalMutationResultDto>> RequestApproval(
        [FromBody] CreateApprovalRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _approvals
            .RequestApprovalAsync(userId, body, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Ok)
            return BadRequest(new ApprovalMutationResultDto
            {
                Succeeded = false,
                ErrorCode = result.ErrorCode,
                Message = result.Message,
            });

        return Ok(new ApprovalMutationResultDto
        {
            Succeeded = true,
            RequestId = result.Dto!.Id,
        });
    }

    [HttpPost("{requestId:guid}/approve")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApprovalMutationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApprovalMutationResultDto>> Approve(
        Guid requestId,
        [FromBody] ResolveApprovalRequestDto? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _approvals
            .ApproveAsync(requestId, userId, body?.Notes, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Ok)
            return BadRequest(new ApprovalMutationResultDto
            {
                Succeeded = false,
                RequestId = requestId,
                ErrorCode = result.ErrorCode,
                Message = result.Message,
            });

        return Ok(new ApprovalMutationResultDto
        {
            Succeeded = true,
            RequestId = requestId,
            ApprovalToken = result.ApprovalToken,
        });
    }

    [HttpPost("{requestId:guid}/reject")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApprovalMutationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApprovalMutationResultDto>> Reject(
        Guid requestId,
        [FromBody] ResolveApprovalRequestDto? body,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _approvals
            .RejectAsync(requestId, userId, body?.Notes, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Ok)
            return BadRequest(new ApprovalMutationResultDto
            {
                Succeeded = false,
                RequestId = requestId,
                ErrorCode = result.ErrorCode,
                Message = result.Message,
            });

        return Ok(new ApprovalMutationResultDto
        {
            Succeeded = true,
            RequestId = requestId,
        });
    }

    /// <summary>Requester claims the single-use approval token after Super Admin approval.</summary>
    [HttpPost("{requestId:guid}/claim")]
    [ProducesResponseType(typeof(ApprovalMutationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApprovalMutationResultDto>> Claim(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _approvals
            .ClaimTokenAsync(requestId, userId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Ok)
            return BadRequest(new ApprovalMutationResultDto
            {
                Succeeded = false,
                RequestId = requestId,
                ErrorCode = result.ErrorCode,
                Message = result.Message,
            });

        return Ok(new ApprovalMutationResultDto
        {
            Succeeded = true,
            RequestId = requestId,
            ApprovalToken = result.ApprovalToken,
        });
    }
}
