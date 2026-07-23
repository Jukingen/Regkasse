using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.CriticalActions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Issues and manages short-lived approval tokens for <see cref="Middleware.CriticalActionMiddleware"/>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/critical-actions")]
[Produces("application/json")]
public sealed class AdminCriticalActionsController : ControllerBase
{
    private readonly ICriticalActionApprovalService _approvals;

    public AdminCriticalActionsController(ICriticalActionApprovalService approvals)
    {
        _approvals = approvals;
    }

    public sealed class IssueWithTwoFactorRequest
    {
        public CriticalActionType ActionType { get; set; }
        public string PathHint { get; set; } = string.Empty;
        public string TwoFactorCode { get; set; } = string.Empty;
    }

    public sealed class RequestApprovalBody
    {
        public CriticalActionType ActionType { get; set; }
        public string PathHint { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public sealed class ApprovalTokenResponse
    {
        public string ApprovalToken { get; set; } = string.Empty;
        public string HeaderName { get; set; } = Configuration.CriticalActionOptions.ApprovalHeaderName;
        public Guid? RequestId { get; set; }
    }

    public sealed class PendingApprovalDto
    {
        public Guid Id { get; set; }
        public string RequesterUserId { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string PathHint { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public DateTime RequestedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    /// <summary>Verify TOTP / Dev bypass and issue a single-use approval token.</summary>
    [HttpPost("approve-with-2fa")]
    [ProducesResponseType(typeof(ApprovalTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveWithTwoFactor(
        [FromBody] IssueWithTwoFactorRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!Enum.IsDefined(body.ActionType))
            return BadRequest(new { code = "INVALID_ACTION_TYPE", message = "Unknown critical action type." });

        var result = await _approvals
            .IssueWithTwoFactorAsync(
                userId,
                body.ActionType,
                body.PathHint,
                body.TwoFactorCode,
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Ok)
            return BadRequest(new { code = result.ErrorCode, message = result.Message });

        return Ok(new ApprovalTokenResponse { ApprovalToken = result.Token! });
    }

    /// <summary>Request Super Admin approval for a critical action (async four-eyes).</summary>
    [HttpPost("request-approval")]
    [ProducesResponseType(typeof(ApprovalTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestApproval(
        [FromBody] RequestApprovalBody? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!Enum.IsDefined(body.ActionType))
            return BadRequest(new { code = "INVALID_ACTION_TYPE", message = "Unknown critical action type." });

        var result = await _approvals
            .RequestSuperAdminApprovalAsync(
                userId,
                body.ActionType,
                body.PathHint,
                body.Reason,
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Ok)
            return BadRequest(new { code = result.ErrorCode, message = result.Message });

        return Ok(new ApprovalTokenResponse { RequestId = result.RequestId });
    }

    /// <summary>List pending Super Admin approval requests.</summary>
    [HttpGet("pending")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(IReadOnlyList<PendingApprovalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPending(CancellationToken cancellationToken)
    {
        var items = await _approvals.ListPendingAsync(cancellationToken).ConfigureAwait(false);
        return Ok(items
            .Select(p => new PendingApprovalDto
            {
                Id = p.Id,
                RequesterUserId = p.RequesterUserId,
                ActionType = p.ActionType.ToString(),
                PathHint = p.PathHint,
                Reason = p.Reason,
                RequestedAtUtc = p.RequestedAtUtc,
                ExpiresAtUtc = p.ExpiresAtUtc,
            })
            .ToList());
    }

    /// <summary>Super Admin approves a pending request and returns a token for the requester.</summary>
    [HttpPost("pending/{requestId:guid}/approve")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApprovalTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApprovePending(Guid requestId, CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _approvals
            .ApprovePendingAsync(requestId, userId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Ok)
            return BadRequest(new { code = result.ErrorCode, message = result.Message });

        return Ok(new ApprovalTokenResponse { ApprovalToken = result.Token!, RequestId = requestId });
    }

    /// <summary>Super Admin rejects a pending request.</summary>
    [HttpPost("pending/{requestId:guid}/reject")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectPending(Guid requestId, CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _approvals
            .RejectPendingAsync(requestId, userId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Ok)
            return BadRequest(new { code = result.ErrorCode, message = result.Message });

        return Ok(new { succeeded = true });
    }
}
