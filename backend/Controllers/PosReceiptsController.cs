using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// POS Belegliste and Nachdruck. Canonical route: <c>api/pos/receipts/*</c>.
/// Reprint returns the persisted receipt only — no new fiscal row and no TSE re-signing.
/// </summary>
[Authorize]
[ApiController]
[Route("api/pos/receipts")]
[Produces("application/json")]
public sealed class PosReceiptsController : ControllerBase
{
    public const int MaxRecentPageSize = 20;

    private readonly IReceiptService _receiptService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PosReceiptsController> _logger;

    public PosReceiptsController(
        IReceiptService receiptService,
        IPaymentService paymentService,
        ILogger<PosReceiptsController> logger)
    {
        _receiptService = receiptService;
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>Last receipts for the current cash register (max 20). Tenant isolation via receipt query filters.</summary>
    [HttpGet]
    [HasPermission(AppPermissions.SaleView)]
    [ProducesResponseType(typeof(PagedResult<ReceiptListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReceiptListItemDto>>> GetRecent(
        [FromQuery] Guid cashRegisterId,
        [FromQuery] int pageSize = MaxRecentPageSize)
    {
        if (cashRegisterId == Guid.Empty)
            return BadRequest(new { message = "cashRegisterId is required." });

        var limit = Math.Clamp(pageSize <= 0 ? MaxRecentPageSize : pageSize, 1, MaxRecentPageSize);
        var result = await _receiptService.GetRecentReceiptsForCashRegisterAsync(cashRegisterId, limit);
        return Ok(result);
    }

    /// <summary>Persisted receipt detail for the current register. Wrong register or tenant → HTTP 404.</summary>
    [HttpGet("{receiptId:guid}")]
    [HasPermission(AppPermissions.SaleView)]
    [ProducesResponseType(typeof(ReceiptDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReceiptDTO>> GetReceipt(
        Guid receiptId,
        [FromQuery] Guid cashRegisterId)
    {
        if (cashRegisterId == Guid.Empty)
            return BadRequest(new { message = "cashRegisterId is required." });

        var receipt = await _receiptService.GetReceiptForCashRegisterAsync(receiptId, cashRegisterId);
        if (receipt == null)
            return NotFound(new { message = "Receipt not found" });
        return Ok(receipt);
    }

    /// <summary>
    /// Nachdruck payload for the POS printer. Audits <c>ReceiptReprintConfirmed</c>; does not create a new Beleg.
    /// <paramref name="receiptId"/> may be the receipt id or the payment id (same as GET /api/Receipts/{id}).
    /// </summary>
    [HttpGet("{receiptId:guid}/reprint")]
    [HasPermission(AppPermissions.ReceiptReprint)]
    [ProducesResponseType(typeof(ReceiptReprintResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ReceiptReprintResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ReceiptReprintResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReceiptReprintResponse>> Reprint(
        Guid receiptId,
        [FromQuery] Guid cashRegisterId,
        [FromQuery] string? reasonCode = null,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            return BadRequest(new { message = "cashRegisterId is required." });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated" });

        var receipt = await _receiptService.GetReceiptForCashRegisterAsync(receiptId, cashRegisterId);
        if (receipt == null)
            return NotFound(new { message = "Receipt not found" });

        var reprintReason = ReceiptReprintReasonCodes.IsValid(reasonCode)
            ? reasonCode!.Trim()
            : ReceiptReprintReasonCodes.CustomerRequest;

        _logger.LogInformation(
            "POS receipt reprint: ReceiptId={ReceiptId}, PaymentId={PaymentId}, CashRegisterId={CashRegisterId}, Reason={Reason}",
            receipt.ReceiptId,
            receipt.PaymentId,
            cashRegisterId,
            reprintReason);

        var result = await _paymentService.ConfirmReceiptReprintAsync(
            receipt.PaymentId,
            new ReceiptReprintRequest { ReprintReasonCode = reprintReason },
            userId,
            cancellationToken).ConfigureAwait(false);

        var response = new ReceiptReprintResponse
        {
            Receipt = result.Receipt ?? receipt,
            Routing = result.Routing,
            AuditLogId = result.AuditLogId?.ToString(),
        };

        if (result.NotFound)
        {
            response.Outcome = "Failed";
            response.ErrorCode = result.ErrorCode;
            response.ErrorMessage = result.ErrorMessage;
            return NotFound(response);
        }

        if (!result.Success)
        {
            response.Outcome = "Failed";
            response.ErrorCode = result.ErrorCode;
            response.ErrorMessage = result.ErrorMessage;
            return BadRequest(response);
        }

        response.Outcome = "Success";
        response.ReportableEventType = "ReceiptReprintConfirmed";
        return Ok(response);
    }
}
