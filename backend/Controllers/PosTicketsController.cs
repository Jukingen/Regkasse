using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/pos/tickets")]
[Produces("application/json")]
[HasPermission(AppPermissions.CartView)]
public sealed class PosTicketsController : ControllerBase
{
    private readonly ITicketRedemptionService _tickets;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IVerticalProfileGuard _profileGuard;

    public PosTicketsController(
        ITicketRedemptionService tickets,
        ICurrentTenantAccessor tenantAccessor,
        IVerticalProfileGuard profileGuard)
    {
        _tickets = tickets;
        _tenantAccessor = tenantAccessor;
        _profileGuard = profileGuard;
    }

    [HttpPost("{code}/validate")]
    [ProducesResponseType(typeof(TicketValidationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketValidationDto>> Validate(string code, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        if (string.IsNullOrWhiteSpace(code))
            return NotFound();

        var ticket = await _tickets.ValidateAsync(code, cancellationToken).ConfigureAwait(false);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost("{code}/redeem")]
    [ProducesResponseType(typeof(TicketValidationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TicketErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TicketErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketValidationDto>> Redeem(string code, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        if (string.IsNullOrWhiteSpace(code))
            return NotFound();

        try
        {
            await _profileGuard.EnforceEndpointAsync("ticketScan", cancellationToken).ConfigureAwait(false);
        }
        catch (ProfileEndpointDisabledException ex)
        {
            return VerticalProfileGuardResponses.From(ex);
        }

        var userId = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var result = await _tickets.RedeemAsync(code, userId, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            TicketRedemptionResult.Ok ok => Ok(ok.Ticket),
            TicketRedemptionResult.NotFound => NotFound(),
            TicketRedemptionResult.Error error => StatusCode(
                error.StatusCode,
                new TicketErrorDto { Code = error.Code, Message = error.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
