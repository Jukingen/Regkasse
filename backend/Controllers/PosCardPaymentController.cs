using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>POS card payment intent lifecycle (Mock / Stripe two-step flow).</summary>
[Authorize(Roles = $"{Roles.Cashier},{Roles.Manager}")]
[ApiController]
[Route("api/pos/card-payment")]
[Route("api/pos/payment/card")]
[Produces("application/json")]
[HasPermission(AppPermissions.PaymentTake)]
public class PosCardPaymentController : ControllerBase
{
    private readonly ICardPaymentService _cardPaymentService;

    public PosCardPaymentController(ICardPaymentService cardPaymentService)
    {
        _cardPaymentService = cardPaymentService;
    }

    [HttpPost("intent")]
    public async Task<ActionResult<CardPaymentIntentResponse>> CreatePaymentIntent(
        [FromBody] CardPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var (response, errorCode, errorMessage) = await _cardPaymentService
            .CreateIntentFromPosRequestAsync(request, userId, cancellationToken)
            .ConfigureAwait(false);

        if (response == null)
            return BadRequest(new { code = errorCode, message = errorMessage });

        return Ok(response);
    }

    [HttpPost("confirm")]
    public async Task<ActionResult<CardPaymentConfirmResponse>> ConfirmPayment(
        [FromBody] ConfirmCardPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var (response, errorCode, errorMessage) = await _cardPaymentService
            .ConfirmByPaymentIntentIdAsync(request, userId, cancellationToken)
            .ConfigureAwait(false);

        if (response == null)
            return errorCode == "CARD_INTENT_NOT_FOUND"
                ? NotFound(new { code = errorCode, message = errorMessage })
                : BadRequest(new { code = errorCode, message = errorMessage });

        if (errorCode == "CARD_CONFIRM_DECLINED")
            return UnprocessableEntity(response);

        return Ok(response);
    }

    /// <summary>Legacy route: confirm by internal intent id.</summary>
    [HttpPost("{intentId:guid}/confirm")]
    public async Task<ActionResult<CardPaymentIntentResponse>> ConfirmIntent(
        Guid intentId,
        [FromBody] ConfirmCardPaymentIntentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var (response, errorCode, errorMessage) = await _cardPaymentService
            .ConfirmIntentAsync(intentId, request, userId, cancellationToken)
            .ConfigureAwait(false);

        if (response == null)
            return errorCode == "CARD_INTENT_NOT_FOUND"
                ? NotFound(new { code = errorCode, message = errorMessage })
                : BadRequest(new { code = errorCode, message = errorMessage });

        if (errorCode == "CARD_CONFIRM_DECLINED")
            return UnprocessableEntity(new { code = errorCode, message = errorMessage, intent = response });

        return Ok(response);
    }

    [HttpPost("{intentId:guid}/cancel")]
    public async Task<ActionResult<CardPaymentIntentResponse>> CancelIntent(
        Guid intentId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var (response, errorCode, errorMessage) = await _cardPaymentService
            .CancelIntentAsync(intentId, userId, cancellationToken)
            .ConfigureAwait(false);

        if (response == null)
            return errorCode == "CARD_INTENT_NOT_FOUND"
                ? NotFound(new { code = errorCode, message = errorMessage })
                : BadRequest(new { code = errorCode, message = errorMessage });

        return Ok(response);
    }
}
