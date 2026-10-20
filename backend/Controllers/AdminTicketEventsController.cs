using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/ticket-events")]
[Produces("application/json")]
public sealed class AdminTicketEventsController : ControllerBase
{
    private readonly ITicketEventService _events;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminTicketEventsController(ITicketEventService events, ICurrentTenantAccessor tenantAccessor)
    {
        _events = events;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [HasPermission(AppPermissions.ProductView)]
    [ProducesResponseType(typeof(IReadOnlyList<TicketEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketEventDto>>> List(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        var items = await _events.ListAsync(activeOnly: false, cancellationToken).ConfigureAwait(false);
        return Ok(items);
    }

    [HttpPost]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(TicketEventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketEventDto>> Create(
        [FromBody] UpsertTicketEventRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var created = await _events.CreateAsync(tenantId, request, cancellationToken).ConfigureAwait(false);
        if (created is null)
            return BadRequest(new { message = "Event window is invalid." });
        return Created($"/api/admin/ticket-events/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(TicketEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketEventDto>> Update(
        Guid id,
        [FromBody] UpsertTicketEventRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid || _tenantAccessor.TenantId == Guid.Empty)
            return NotFound();
        var updated = await _events.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        return updated switch
        {
            TicketEventUpdateResult.Ok ok => Ok(ok.Event),
            TicketEventUpdateResult.Invalid invalid => BadRequest(new { message = invalid.Message }),
            _ => NotFound(),
        };
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid || _tenantAccessor.TenantId == Guid.Empty)
            return NotFound();
        var ok = await _events.DeactivateAsync(id, cancellationToken).ConfigureAwait(false);
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/seats")]
    [HasPermission(AppPermissions.ProductView)]
    [ProducesResponseType(typeof(IReadOnlyList<TicketSeatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketSeatDto>>> Seats(Guid id, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid || _tenantAccessor.TenantId == Guid.Empty)
            return NotFound();
        var seats = await _events.ListSeatsAsync(id, cancellationToken).ConfigureAwait(false);
        return seats is null ? NotFound() : Ok(seats);
    }

    [HttpPost("{id:guid}/seats")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(TicketSeatDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(TicketErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TicketErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddSeat(
        Guid id,
        [FromBody] CreateTicketSeatRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid || _tenantAccessor.TenantId == Guid.Empty)
            return NotFound();
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var result = await _events.AddSeatAsync(id, request, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            TicketSeatActionResult.OkSeat ok => Created($"/api/admin/ticket-events/{id}/seats", ok.Seat),
            TicketSeatActionResult.NotFound => NotFound(),
            TicketSeatActionResult.Error error => StatusCode(
                error.StatusCode,
                new TicketErrorDto { Code = error.Code, Message = error.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    [HttpGet("{id:guid}/category-prices")]
    [HasPermission(AppPermissions.ProductView)]
    [ProducesResponseType(typeof(IReadOnlyList<TicketEventCategoryPriceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketEventCategoryPriceDto>>> CategoryPrices(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid || _tenantAccessor.TenantId == Guid.Empty)
            return NotFound();
        var prices = await _events.ListCategoryPricesAsync(id, cancellationToken).ConfigureAwait(false);
        return prices is null ? NotFound() : Ok(prices);
    }

    [HttpPut("{id:guid}/category-prices")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(TicketEventCategoryPriceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TicketErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCategoryPrice(
        Guid id,
        [FromBody] SetTicketEventCategoryPriceRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid || _tenantAccessor.TenantId == Guid.Empty)
            return NotFound();
        var result = await _events.SetCategoryPriceAsync(id, request, ActorUserId(), cancellationToken).ConfigureAwait(false);
        return FromCatalog(result);
    }

    private string ActorUserId() =>
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? string.Empty;

    private static IActionResult FromCatalog(TicketCatalogResult result) => result switch
    {
        TicketCatalogResult.PriceOk ok => new OkObjectResult(ok.Price),
        TicketCatalogResult.CategoryOk ok => new OkObjectResult(ok.Category),
        TicketCatalogResult.NotFound => new NotFoundResult(),
        TicketCatalogResult.Error error => new ObjectResult(new TicketErrorDto
        {
            Code = error.Code,
            Message = error.Message,
        })
        { StatusCode = error.StatusCode },
        _ => new StatusCodeResult(StatusCodes.Status500InternalServerError),
    };
}
