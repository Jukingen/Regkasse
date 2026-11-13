using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Feedback;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// FA user feedback loop: submit + track own items; Super Admin reviews all.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/feedback")]
[Produces("application/json")]
public sealed class AdminFeedbackController : ControllerBase
{
    private readonly IAdminFeedbackService _feedback;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminFeedbackController(
        IAdminFeedbackService feedback,
        ICurrentTenantAccessor tenantAccessor)
    {
        _feedback = feedback;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>Submit feedback (any authenticated admin user with tenant context).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminFeedbackDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminFeedbackDto>> Create(
        [FromBody] CreateAdminFeedbackRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return BadRequest(new { code = "TENANT_CONTEXT_REQUIRED", message = "Tenant context is required to submit feedback." });

        try
        {
            var displayName =
                User.FindFirstValue("name")
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? User.Identity?.Name;

            var created = await _feedback
                .CreateAsync(tenantId.Value, userId, displayName, body, cancellationToken)
                .ConfigureAwait(false);
            return CreatedAtAction(nameof(ListMine), new { }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_FEEDBACK", message = ex.Message });
        }
    }

    /// <summary>List feedback submitted by the current user (status closes the loop).</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(AdminFeedbackListResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminFeedbackListResponseDto>> ListMine(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _feedback.ListMineAsync(userId, limit, offset, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Super Admin inbox: all tenants.</summary>
    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(AdminFeedbackListResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminFeedbackListResponseDto>> ListAll(
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var result = await _feedback
            .ListAllAsync(status, category, limit, offset, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Update status / reviewer note (Implemented, In Progress, Under Review, …).</summary>
    [HttpPatch("{id:guid}/status")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(AdminFeedbackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminFeedbackDto>> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateAdminFeedbackStatusRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var updated = await _feedback
                .UpdateStatusAsync(id, userId, body, cancellationToken)
                .ConfigureAwait(false);
            if (updated is null)
                return NotFound();
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_STATUS", message = ex.Message });
        }
    }
}
