using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin support inbox (all tenants). Ambient tenant is not required
/// (<see cref="KasseAPI_Final.Middleware.TenantValidationMiddleware"/> <c>/api/admin/support</c>).
/// Missing ticket → HTTP 404.
/// </summary>
[ApiController]
[Route("api/admin/support/admin/tickets")]
[Authorize]
[HasPermission(AppPermissions.SystemCritical)]
[Produces("application/json")]
public sealed class AdminSupportInboxController : ControllerBase
{
    private readonly ISupportTicketService _tickets;

    public AdminSupportInboxController(ISupportTicketService tickets)
    {
        _tickets = tickets;
    }

    [HttpGet]
    [ProducesResponseType(typeof(SupportTicketListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] string? priority = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _tickets
            .ListAllAsync(
                new SupportTicketListQuery
                {
                    Page = page,
                    PageSize = pageSize,
                    Status = status,
                    Category = category,
                    Priority = priority,
                    Search = search,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                },
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(SupportTicketInboxSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var summary = await _tickets.GetInboxSummaryAsync(cancellationToken).ConfigureAwait(false);
        return Ok(summary);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SupportTicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _tickets.GetAnyAsync(id, cancellationToken).ConfigureAwait(false);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType(typeof(SupportTicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMessage(
        Guid id,
        [FromBody] AddSupportTicketMessageRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var dto = await _tickets
                .AddStaffMessageAsync(
                    id,
                    userId,
                    DisplayName(),
                    body.Body,
                    body.IsInternal,
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_MESSAGE", message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(SupportTicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateSupportTicketStatusRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        try
        {
            var dto = await _tickets.UpdateStatusAsync(id, body.Status, cancellationToken).ConfigureAwait(false);
            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_STATUS", message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/assign")]
    [ProducesResponseType(typeof(SupportTicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(
        Guid id,
        [FromBody] AssignSupportTicketRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });

        var assignedTo = string.IsNullOrWhiteSpace(body.AssignedToUserId)
            ? User.GetActorUserId()
            : body.AssignedToUserId.Trim();
        if (string.IsNullOrEmpty(assignedTo))
            return Unauthorized();

        try
        {
            var dto = await _tickets
                .AssignTicketAsync(id, assignedTo, DisplayName(), cancellationToken)
                .ConfigureAwait(false);
            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_ASSIGNMENT", message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private string? DisplayName() =>
        User.FindFirstValue("name")
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? User.Identity?.Name;
}
