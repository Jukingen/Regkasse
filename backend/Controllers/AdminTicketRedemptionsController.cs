using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/tickets/redemptions")]
[Produces("application/json")]
[HasPermission(AppPermissions.ProductView)]
public sealed class AdminTicketRedemptionsController : ControllerBase
{
    private readonly ITicketRedemptionService _tickets;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminTicketRedemptionsController(
        ITicketRedemptionService tickets,
        ICurrentTenantAccessor tenantAccessor)
    {
        _tickets = tickets;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(TicketRedemptionListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketRedemptionListResponse>> List(
        [FromQuery] string? status,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid ambient || ambient == Guid.Empty)
            return NotFound();

        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        Guid? filterTenant = ambient;
        if (isSuperAdmin && tenantId is Guid requested && requested != Guid.Empty)
            filterTenant = requested;

        TicketRedemptionStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<TicketRedemptionStatus>(status, ignoreCase: true, out var value))
        {
            parsed = value;
        }

        var items = await _tickets.ListAsync(filterTenant, parsed, cancellationToken).ConfigureAwait(false);
        return Ok(new TicketRedemptionListResponse { Items = items });
    }
}
