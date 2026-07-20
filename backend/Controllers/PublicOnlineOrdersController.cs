using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Anonymous customer order placement, status lookup, and online checkout.
/// Placement is gated by tenant working hours (website/app only — never POS/FA).
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/public/online-orders")]
[Produces("application/json")]
public sealed class PublicOnlineOrdersController : ControllerBase
{
    private readonly IOnlineOrderQueryService _queries;
    private readonly IOnlineOrderPaymentService _payments;
    private readonly IOnlineOrderIntakeService _intake;

    public PublicOnlineOrdersController(
        IOnlineOrderQueryService queries,
        IOnlineOrderPaymentService payments,
        IOnlineOrderIntakeService intake)
    {
        _queries = queries;
        _payments = payments;
        _intake = intake;
    }

    /// <summary>
    /// Place a new online order (website/PWA/native). Returns 409 when closed / past cutoff.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreatePublicOnlineOrderResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CreatePublicOnlineOrderResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(CreatePublicOnlineOrderResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(CreatePublicOnlineOrderResponseDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatePublicOnlineOrderResponseDto>> Create(
        [FromBody] CreatePublicOnlineOrderRequestDto? body,
        CancellationToken ct = default)
    {
        if (body is null)
        {
            return BadRequest(new CreatePublicOnlineOrderResponseDto
            {
                Succeeded = false,
                Code = OnlineOrderIntakeService.ValidationCode,
                Error = "Request body is required.",
            });
        }

        var result = await _intake.CreateAsync(body, ct);
        if (result.Succeeded)
            return StatusCode(StatusCodes.Status201Created, result);

        return result.Code switch
        {
            OnlineOrderIntakeService.TenantNotFoundCode => NotFound(result),
            OnlineOrderIntakeService.ClosedCode => Conflict(result),
            _ => BadRequest(result),
        };
    }

    /// <summary>
    /// Lookup by tenant slug + order number. Optional phone (digits) must match when provided.
    /// Returns 404 for unknown / mismatched lookups (no enumeration).
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(PublicOnlineOrderStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PublicOnlineOrderStatusDto>> GetStatus(
        [FromQuery] string? tenant = null,
        [FromQuery] string? orderNumber = null,
        [FromQuery] string? phone = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenant) || string.IsNullOrWhiteSpace(orderNumber))
        {
            return BadRequest(new { message = "tenant and orderNumber are required." });
        }

        var status = await _queries.GetPublicStatusAsync(tenant, orderNumber, phone, ct);
        if (status is null)
            return NotFound();

        return Ok(status);
    }

    /// <summary>Create PaymentIntent for online checkout (requires tenant slug).</summary>
    [HttpPost("{orderId:guid}/payment-intent")]
    [ProducesResponseType(typeof(OnlineOrderPaymentIntentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OnlineOrderPaymentIntentResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OnlineOrderPaymentIntentResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnlineOrderPaymentIntentResponseDto>> CreatePaymentIntent(
        [FromRoute] Guid orderId,
        [FromBody] CreateOnlineOrderPaymentIntentRequestDto? body,
        [FromQuery] string? tenant = null,
        CancellationToken ct = default)
    {
        var slug = body?.Tenant ?? tenant;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return BadRequest(new OnlineOrderPaymentIntentResponseDto
            {
                Succeeded = false,
                Code = "TENANT_REQUIRED",
                Error = "tenant slug is required."
            });
        }

        var result = await _payments.CreatePaymentIntentAsync(orderId, slug, ct);
        return MapPaymentResult(result);
    }

    /// <summary>Confirm PaymentIntent after client-side payment (Mock auto-succeeds; Stripe when status=succeeded).</summary>
    [HttpPost("payments/confirm")]
    [ProducesResponseType(typeof(OnlineOrderPaymentIntentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OnlineOrderPaymentIntentResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OnlineOrderPaymentIntentResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnlineOrderPaymentIntentResponseDto>> ConfirmPayment(
        [FromBody] ConfirmOnlineOrderPaymentRequestDto? body,
        CancellationToken ct = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.PaymentIntentId))
        {
            return BadRequest(new OnlineOrderPaymentIntentResponseDto
            {
                Succeeded = false,
                Code = "VALIDATION_ERROR",
                Error = "PaymentIntentId is required."
            });
        }

        var result = await _payments.ConfirmPaymentAsync(body.PaymentIntentId, body.PaymentMethodId, ct);
        return MapPaymentResult(result);
    }

    private ActionResult<OnlineOrderPaymentIntentResponseDto> MapPaymentResult(OnlineOrderPaymentResult result)
    {
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
                OnlineOrderPaymentService.OrderNotFoundCode
                    or OnlineOrderPaymentService.IntentNotFoundCode => NotFound(dto),
                _ => BadRequest(dto)
            };
        }

        return Ok(dto);
    }
}
