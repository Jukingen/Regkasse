using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>FA online-order inbox (website / PWA / native). Not fiscal POS receipts.</summary>
[Authorize]
[ApiController]
[Route("api/admin/online-orders")]
[Produces("application/json")]
public sealed class AdminOnlineOrdersController : ControllerBase
{
    private readonly IOnlineOrderQueryService _queries;
    private readonly IOrderIntegrationService _integration;
    private readonly IOnlineOrderStatusService _status;
    private readonly IOnlineOrderPaymentService _payments;
    private readonly IOnlineOrderTrackingService _tracking;

    public AdminOnlineOrdersController(
        IOnlineOrderQueryService queries,
        IOrderIntegrationService integration,
        IOnlineOrderStatusService status,
        IOnlineOrderPaymentService payments,
        IOnlineOrderTrackingService tracking)
    {
        _queries = queries;
        _integration = integration;
        _status = status;
        _payments = payments;
        _tracking = tracking;
    }

    [HttpGet]
    [HasPermission(AppPermissions.DigitalOrdersView)]
    [ProducesResponseType(typeof(OnlineOrderListResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OnlineOrderListResponseDto>> List(
        [FromQuery] string? status = null,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var result = await _queries.ListAsync(status, take, ct);
        return Ok(result);
    }

    [HttpGet("analytics")]
    [HasPermission(AppPermissions.DigitalOrdersView)]
    [ProducesResponseType(typeof(OnlineOrderAnalyticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OnlineOrderAnalyticsDto>> Analytics(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken ct = default)
    {
        var result = await _queries.GetAnalyticsAsync(fromUtc, toUtc, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.DigitalOrdersView)]
    [ProducesResponseType(typeof(OnlineOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnlineOrderDto>> GetById(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var order = await _queries.GetByIdAsync(id, ct);
        if (order is null)
            return NotFound();
        return Ok(order);
    }

    /// <summary>
    /// Optional POS cart bridge (creates a POS cart for the actor). Requires
    /// <see cref="AppPermissions.DigitalOrdersApprove"/> (Super Admin) — not Manager status-only flow.
    /// Prefer <see cref="UpdateStatus"/> for website/app fulfillment.
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [HasPermission(AppPermissions.DigitalOrdersApprove)]
    [ProducesResponseType(typeof(AcceptOnlineOrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AcceptOnlineOrderResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AcceptOnlineOrderResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AcceptOnlineOrderResponseDto>> Accept(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var actor = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(actor))
        {
            return BadRequest(new AcceptOnlineOrderResponseDto
            {
                Succeeded = false,
                Code = OrderIntegrationService.ClaimingUserRequiredCode,
                Error = "Authenticated user id is required."
            });
        }

        var result = await _integration.PushOrderToPosAsync(id, actor, ct);
        if (!result.Succeeded)
        {
            var fail = new AcceptOnlineOrderResponseDto
            {
                Succeeded = false,
                Code = result.Code,
                Error = result.Error,
                PosCartId = result.PosCartId,
                AlreadyPushed = result.AlreadyPushed
            };
            return result.Code switch
            {
                OrderIntegrationService.OrderNotFoundCode => NotFound(fail),
                _ => BadRequest(fail)
            };
        }

        return Ok(new AcceptOnlineOrderResponseDto
        {
            Succeeded = true,
            PosCartId = result.PosCartId,
            AlreadyPushed = result.AlreadyPushed,
            Order = await _queries.GetByIdAsync(id, ct)
        });
    }

    /// <summary>
    /// Update kitchen/fulfillment status only (no POS cart, no TSE, no fiscal receipt).
    /// Allowed: pending→accepted→preparing→ready→completed, or cancel from non-terminal states.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [HasPermission(AppPermissions.DigitalOrdersManage)]
    [ProducesResponseType(typeof(UpdateOnlineOrderStatusResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UpdateOnlineOrderStatusResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UpdateOnlineOrderStatusResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpdateOnlineOrderStatusResponseDto>> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateOnlineOrderStatusRequestDto? body,
        CancellationToken ct = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Status))
        {
            return BadRequest(new UpdateOnlineOrderStatusResponseDto
            {
                Succeeded = false,
                Code = OnlineOrderStatusService.InvalidStatusCode,
                Error = "Status is required."
            });
        }

        var result = await _status.UpdateStatusAsync(id, body.Status, User.GetActorUserId(), ct);
        var dto = new UpdateOnlineOrderStatusResponseDto
        {
            Succeeded = result.Succeeded,
            Code = result.Code,
            Error = result.Error,
            Order = result.Succeeded ? await _queries.GetByIdAsync(id, ct) : null
        };

        if (!result.Succeeded)
        {
            return result.Code switch
            {
                OnlineOrderStatusService.OrderNotFoundCode => NotFound(dto),
                _ => BadRequest(dto)
            };
        }

        return Ok(dto);
    }

    [HttpGet("{id:guid}/timeline")]
    [HasPermission(AppPermissions.DigitalOrdersView)]
    [ProducesResponseType(typeof(IReadOnlyList<OnlineOrderStatusChangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OnlineOrderStatusChangeDto>>> Timeline(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var order = await _queries.GetByIdAsync(id, ct);
        if (order is null)
            return NotFound();

        var timeline = await _tracking.GetTimelineAsync(id, ct);
        return Ok(timeline);
    }

    /// <summary>Create payment intent for an online order (staff-assisted checkout).</summary>
    [HttpPost("{id:guid}/payment-intent")]
    [HasPermission(AppPermissions.DigitalOrdersManage)]
    [ProducesResponseType(typeof(OnlineOrderPaymentIntentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OnlineOrderPaymentIntentResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OnlineOrderPaymentIntentResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnlineOrderPaymentIntentResponseDto>> CreatePaymentIntent(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var result = await _payments.CreatePaymentIntentAsync(id, tenantSlug: null, ct);
        var dto = new OnlineOrderPaymentIntentResponseDto
        {
            Succeeded = result.Succeeded,
            Code = result.Code,
            Error = result.Error,
            OrderId = result.Order?.Id,
            OrderNumber = result.Order?.OrderNumber,
            PaymentIntentId = result.PaymentIntentId,
            ClientSecret = result.ClientSecret,
            Provider = result.Provider,
            Amount = result.Order?.Total,
            Currency = "EUR"
        };

        if (!result.Succeeded)
        {
            return result.Code switch
            {
                OnlineOrderPaymentService.OrderNotFoundCode => NotFound(dto),
                _ => BadRequest(dto)
            };
        }

        return Ok(dto);
    }
}
