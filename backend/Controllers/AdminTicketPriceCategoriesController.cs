using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/ticket-price-categories")]
[Produces("application/json")]
public sealed class AdminTicketPriceCategoriesController : ControllerBase
{
    private readonly ITicketEventService _events;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminTicketPriceCategoriesController(ITicketEventService events, ICurrentTenantAccessor tenantAccessor)
    {
        _events = events;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [HasPermission(AppPermissions.ProductView)]
    [ProducesResponseType(typeof(IReadOnlyList<TicketPriceCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketPriceCategoryDto>>> List(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        var items = await _events.ListCategoriesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.ProductView)]
    [ProducesResponseType(typeof(TicketPriceCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketPriceCategoryDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        var item = await _events.GetCategoryAsync(id, cancellationToken).ConfigureAwait(false);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(TicketPriceCategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(TicketErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TicketErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] UpsertTicketPriceCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var result = await _events.CreateCategoryAsync(tenantId, request, ActorUserId(), cancellationToken)
            .ConfigureAwait(false);
        if (result is TicketCatalogResult.CategoryOk ok)
            return Created($"/api/admin/ticket-price-categories/{ok.Category.Id}", ok.Category);
        return From(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(TicketPriceCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TicketErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TicketErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpsertTicketPriceCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid || _tenantAccessor.TenantId == Guid.Empty)
            return NotFound();
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var result = await _events.UpdateCategoryAsync(id, request, ActorUserId(), cancellationToken)
            .ConfigureAwait(false);
        return From(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid || _tenantAccessor.TenantId == Guid.Empty)
            return NotFound();
        var result = await _events.DeactivateCategoryAsync(id, ActorUserId(), cancellationToken).ConfigureAwait(false);
        return result switch
        {
            TicketCatalogResult.CategoryOk => NoContent(),
            TicketCatalogResult.NotFound => NotFound(),
            TicketCatalogResult.Error error => StatusCode(error.StatusCode, new TicketErrorDto
            {
                Code = error.Code,
                Message = error.Message,
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private string ActorUserId() =>
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? string.Empty;

    private static IActionResult From(TicketCatalogResult result) => result switch
    {
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
