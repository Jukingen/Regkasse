using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Kitchen;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/pos/kitchen-orders")]
[Produces("application/json")]
public sealed class PosKitchenOrdersController : ControllerBase
{
    private readonly IKitchenOrderService _orders;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PosKitchenOrdersController(IKitchenOrderService orders, ICurrentTenantAccessor tenantAccessor)
    {
        _orders = orders;
        _tenantAccessor = tenantAccessor;
    }

    [HttpPost]
    [HasPermission(AppPermissions.CartView)]
    [ProducesResponseType(typeof(KitchenOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(KitchenOrderErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KitchenOrderDto>> Create(
        [FromBody] CreateKitchenOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _orders
            .CreateAsync(request, User.GetActorUserId(), User.GetActorRole(), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpGet]
    [HasPermission(AppPermissions.KitchenView)]
    [ProducesResponseType(typeof(IReadOnlyList<KitchenOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<KitchenOrderDto>>> List(
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        KitchenOrderStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<KitchenOrderStatus>(status, ignoreCase: true, out var value))
        {
            parsed = value;
        }

        var items = await _orders.ListAsync(parsed, from, to, cancellationToken).ConfigureAwait(false);
        return Ok(items);
    }

    [HttpPatch("{id:guid}/status")]
    [HasPermission(AppPermissions.KitchenUpdate)]
    [ProducesResponseType(typeof(KitchenOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(KitchenOrderErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KitchenOrderDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateKitchenOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var result = await _orders
            .UpdateStatusAsync(id, request.Status, User.GetActorUserId(), User.GetActorRole(), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPatch("{id:guid}/items/{itemId:guid}/status")]
    [HasPermission(AppPermissions.KitchenUpdate)]
    [ProducesResponseType(typeof(KitchenOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(KitchenOrderErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KitchenOrderDto>> UpdateItemStatus(
        Guid id,
        Guid itemId,
        [FromBody] UpdateKitchenOrderItemStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var result = await _orders
            .UpdateItemStatusAsync(id, itemId, request.Status, User.GetActorUserId(), User.GetActorRole(), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.CartView)]
    [ProducesResponseType(typeof(KitchenOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(KitchenOrderErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KitchenOrderDto>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var result = await _orders
            .CancelAsync(id, User.GetActorUserId(), User.GetActorRole(), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    private ActionResult<KitchenOrderDto> ToActionResult(KitchenOrderWriteResult result) =>
        result switch
        {
            KitchenOrderWriteResult.Ok ok when ok.StatusCode == StatusCodes.Status201Created =>
                Created($"/api/pos/kitchen-orders/{ok.Order.Id:D}", ok.Order),
            KitchenOrderWriteResult.Ok ok => Ok(ok.Order),
            KitchenOrderWriteResult.NotFound => NotFound(),
            KitchenOrderWriteResult.Error error => StatusCode(
                error.StatusCode,
                new KitchenOrderErrorDto { Code = error.Code, Message = error.Message }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
