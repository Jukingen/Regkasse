using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/pos/ticket-events")]
[Produces("application/json")]
[HasPermission(AppPermissions.CartView)]
public sealed class PosTicketEventsController : ControllerBase
{
    private readonly ITicketEventService _events;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IVerticalProfileGuard _profileGuard;

    public PosTicketEventsController(
        ITicketEventService events,
        ICurrentTenantAccessor tenantAccessor,
        IVerticalProfileGuard profileGuard)
    {
        _events = events;
        _tenantAccessor = tenantAccessor;
        _profileGuard = profileGuard;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TicketEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketEventDto>>> List(CancellationToken cancellationToken)
    {
        if (!await AllowAsync(cancellationToken).ConfigureAwait(false))
            return NotFound();
        var items = await _events.ListAsync(activeOnly: true, cancellationToken).ConfigureAwait(false);
        return Ok(items);
    }

    [HttpGet("{id:guid}/seats")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketSeatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketSeatDto>>> Seats(Guid id, CancellationToken cancellationToken)
    {
        if (!await AllowAsync(cancellationToken).ConfigureAwait(false))
            return NotFound();
        var seats = await _events.ListSeatsAsync(id, cancellationToken).ConfigureAwait(false);
        return seats is null ? NotFound() : Ok(seats);
    }

    [HttpPost("{id:guid}/seats/reserve")]
    [ProducesResponseType(typeof(TicketSeatDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TicketErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reserve(
        Guid id,
        [FromBody] SeatActionRequest request,
        CancellationToken cancellationToken)
    {
        if (!await AllowAsync(cancellationToken).ConfigureAwait(false))
            return NotFound();
        var result = await _events.ReserveAsync(id, request.SeatId, cancellationToken).ConfigureAwait(false);
        return From(result);
    }

    [HttpPost("{id:guid}/seats/sell")]
    [ProducesResponseType(typeof(SoldTicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TicketErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sell(
        Guid id,
        [FromBody] SeatActionRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        if (!await ProfileAllowsAsync(cancellationToken).ConfigureAwait(false))
            return NotFound();
        var result = await _events.SellAsync(tenantId, id, request.SeatId, cancellationToken).ConfigureAwait(false);
        return From(result);
    }

    private async Task<bool> AllowAsync(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return false;
        return await ProfileAllowsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ProfileAllowsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _profileGuard.EnforceEndpointAsync("ticketScan", cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ProfileEndpointDisabledException)
        {
            return false;
        }
    }

    private static IActionResult From(TicketSeatActionResult result) => result switch
    {
        TicketSeatActionResult.OkSeat ok => new OkObjectResult(ok.Seat),
        TicketSeatActionResult.OkSale ok => new OkObjectResult(ok.Ticket),
        TicketSeatActionResult.NotFound => new NotFoundResult(),
        TicketSeatActionResult.Error error => new ObjectResult(new TicketErrorDto
        {
            Code = error.Code,
            Message = error.Message,
        })
        { StatusCode = error.StatusCode },
        _ => new StatusCodeResult(StatusCodes.Status500InternalServerError),
    };
}
