using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Preorder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// POS Vorbestellung lifecycle. Fiscal receipt is created at payment time;
/// pickup only updates status. Cancel uses existing storno.
/// </summary>
[Authorize]
[ApiController]
[Route("api/pos/orders")]
[Produces("application/json")]
public sealed class PosPreorderController : ControllerBase
{
    private readonly IPreorderService _preorders;
    private readonly IPaymentService _payments;

    public PosPreorderController(IPreorderService preorders, IPaymentService payments)
    {
        _preorders = preorders;
        _payments = payments;
    }

    [HttpGet("preorders")]
    [HasPermission(AppPermissions.OrderView)]
    [ProducesResponseType(typeof(PreorderListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PreorderListResponseDto>> List(
        [FromQuery] string? status = null,
        [FromQuery] string? receiptNumber = null,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _preorders.ListAsync(status, receiptNumber, take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("preorders/by-receipt/{receiptNumber}")]
    [HasPermission(AppPermissions.OrderView)]
    [ProducesResponseType(typeof(PreorderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PreorderDto>> GetByReceipt(
        [FromRoute] string receiptNumber,
        CancellationToken cancellationToken = default)
    {
        var order = await _preorders.GetByReceiptNumberAsync(receiptNumber, cancellationToken);
        if (order is null)
            return NotFound();
        return Ok(order);
    }

    [HttpPut("{id:guid}/preorder-status")]
    [ProducesResponseType(typeof(PreorderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PreorderDto>> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdatePreorderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var status = PreorderStatuses.Normalize(request.Status);
        if (status == PreorderStatuses.Cancelled)
            return await CancelViaStornoAsync(id, request, cancellationToken);

        if (!User.HasPermissionClaim(AppPermissions.OrderUpdate))
            return Forbid();

        try
        {
            var updated = await _preorders.SetOperationalStatusAsync(id, status, cancellationToken);
            if (updated is null)
                return NotFound();
            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message is "PREORDER_STATUS_TRANSITION_DENIED")
        {
            return BadRequest(new { message = "Pre-order status transition is not allowed.", code = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message is "PREORDER_BALANCE_OPEN")
        {
            return BadRequest(new
            {
                message = "Offener Betrag muss zuerst fiskalisch bezahlt werden.",
                code = ex.Message
            });
        }
    }

    private async Task<ActionResult<PreorderDto>> CancelViaStornoAsync(
        Guid id,
        UpdatePreorderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.HasPermissionClaim(AppPermissions.OrderCancel)
            || !User.HasPermissionClaim(AppPermissions.PaymentCancel))
        {
            return Forbid();
        }

        var existing = await _preorders.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return NotFound();

        if (existing.Status == PreorderStatuses.Cancelled)
            return Ok(existing);

        if (existing.SourcePaymentId is null || existing.SourcePaymentId == Guid.Empty)
            return BadRequest(new { message = "Pre-order has no fiscal payment to storno.", code = "PREORDER_PAYMENT_MISSING" });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var reason = string.IsNullOrWhiteSpace(request.CancellationReason)
            ? "Kunde storniert (Vorbestellung)"
            : request.CancellationReason.Trim();

        var result = await _payments.CancelPaymentAsync(
            existing.SourcePaymentId.Value,
            reason,
            userId,
            idempotencyKey: null,
            reasonCode: CancellationReasonCode.CustomerRequest);

        if (!result.Success)
        {
            return BadRequest(new
            {
                message = result.Message,
                code = result.DiagnosticCode,
                errors = result.Errors
            });
        }

        var updated = await _preorders.GetByIdAsync(id, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }
}
