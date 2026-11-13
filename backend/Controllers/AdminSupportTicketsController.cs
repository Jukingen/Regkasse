using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Support;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Mandanten-Admin support tickets (own tenant). Missing / cross-tenant → HTTP 404.
/// Permission: <see cref="AppPermissions.LicenseManage"/>.
/// Super Admin inbox lives at <c>/api/admin/support/admin/tickets</c>.
/// </summary>
[ApiController]
[Route("api/admin/support/tickets")]
[Authorize]
[Produces("application/json")]
public sealed class AdminSupportTicketsController : ControllerBase
{
    private readonly ISupportTicketService _tickets;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminSupportTicketsController(
        ISupportTicketService tickets,
        ICurrentTenantAccessor tenantAccessor)
    {
        _tickets = tickets;
        _tenantAccessor = tenantAccessor;
    }

    [HttpPost]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(SupportTicketDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupportTicketRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });
        if (!TryGetAmbientTenantId(out var tenantId))
            return NotFound();

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var created = await _tickets
                .CreateAsync(tenantId, userId, DisplayName(), body, cancellationToken)
                .ConfigureAwait(false);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "INVALID_TICKET", message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(SupportTicketListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetAmbientTenantId(out var tenantId))
            return NotFound();

        var result = await _tickets
            .ListForTenantAsync(
                tenantId,
                new SupportTicketListQuery
                {
                    Page = page,
                    PageSize = pageSize,
                    Status = status,
                    Category = category,
                },
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("open-count")]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OpenCount(CancellationToken cancellationToken)
    {
        if (!TryGetAmbientTenantId(out var tenantId))
            return NotFound();

        var count = await _tickets.GetOpenTicketCountAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(new { openCount = count });
    }

    /// <summary>Kept for Super Admin clients that still call the legacy inbox path.</summary>
    [HttpGet("all")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(SupportTicketListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAllLegacy(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] string? priority = null,
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
                },
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(SupportTicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (User.IsInRole(Roles.SuperAdmin))
            {
                var any = await _tickets.GetAnyAsync(id, cancellationToken).ConfigureAwait(false);
                return Ok(any);
            }

            if (!TryGetAmbientTenantId(out var tenantId))
                return NotFound();

            var dto = await _tickets.GetForTenantAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/messages")]
    [HasPermission(AppPermissions.LicenseManage)]
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
            if (User.IsInRole(Roles.SuperAdmin))
            {
                var staff = await _tickets
                    .AddStaffMessageAsync(
                        id,
                        userId,
                        DisplayName(),
                        body.Body,
                        body.IsInternal,
                        cancellationToken)
                    .ConfigureAwait(false);
                return Ok(staff);
            }

            if (!TryGetAmbientTenantId(out var tenantId))
                return NotFound();

            var dto = await _tickets
                .AddMessageForTenantAsync(tenantId, id, userId, DisplayName(), body.Body, cancellationToken)
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
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(SupportTicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOwnStatus(
        Guid id,
        [FromBody] UpdateSupportTicketStatusRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new { code = "INVALID_BODY", message = "Request body is required." });
        if (!TryGetAmbientTenantId(out var tenantId))
            return NotFound();

        try
        {
            var dto = await _tickets
                .UpdateStatusForTenantAsync(tenantId, id, body.Status, cancellationToken)
                .ConfigureAwait(false);
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

    private bool TryGetAmbientTenantId(out Guid tenantId)
    {
        if (_tenantAccessor.TenantId is Guid id && id != Guid.Empty)
        {
            tenantId = id;
            return true;
        }

        tenantId = Guid.Empty;
        return false;
    }

    private string? DisplayName() =>
        User.FindFirstValue("name")
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? User.Identity?.Name;
}
