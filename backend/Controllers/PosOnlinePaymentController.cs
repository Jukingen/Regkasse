using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>POS online payment initiation (card / PayPal). Fiscal commit remains <c>POST /api/pos/payment</c>.</summary>
[Authorize(Roles = $"{Roles.Cashier},{Roles.Manager}")]
[ApiController]
[Route("api/pos/payment")]
[Produces("application/json")]
[HasPermission(AppPermissions.PaymentTake)]
public sealed class PosOnlinePaymentController : ControllerBase
{
    private readonly IPaymentGatewayService _payments;

    public PosOnlinePaymentController(IPaymentGatewayService payments)
    {
        _payments = payments;
    }

    /// <summary>Starts an online payment and returns a payment intent and/or hosted redirect URL.</summary>
    [HttpPost("initiate")]
    [ProducesResponseType(typeof(OnlinePaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OnlinePaymentDto>> Initiate(
        [FromBody] InitiateOnlinePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _payments.ProcessPaymentAsync(request, userId, cancellationToken)
            .ConfigureAwait(false);
        return MapResult(result);
    }

    /// <summary>Poll current online-payment flow state (tenant-scoped; cross-tenant → 404).</summary>
    [HttpGet("initiate/{id:guid}")]
    [HttpGet("online/{id:guid}")]
    [ProducesResponseType(typeof(OnlinePaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnlinePaymentDto>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _payments.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return MapResult(result);
    }

    private ActionResult<OnlinePaymentDto> MapResult(PaymentGatewayOperationResult result)
    {
        if (result.Ok && result.Payment != null)
            return Ok(result.Payment);

        var body = new { code = result.ErrorCode, message = result.ErrorMessage };
        if (result.IsNotFound
            || string.Equals(result.ErrorCode, OnlinePaymentErrorCodes.TenantContextRequired, StringComparison.Ordinal)
            || string.Equals(result.ErrorCode, CashRegisterResolutionCodes.NotFound, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(body);
        }

        if (string.Equals(result.ErrorCode, CashRegisterResolutionCodes.Decommissioned, StringComparison.Ordinal))
            return Conflict(body);

        return BadRequest(body);
    }
}
