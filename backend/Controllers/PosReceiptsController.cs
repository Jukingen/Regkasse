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
    private readonly IFiskalyReceiptService _fiskalyReceipts;
    private readonly IUserService _userService;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<PosReceiptsController> _logger;

    public PosReceiptsController(
        IReceiptService receiptService,
        IPaymentService paymentService,
        IFiskalyReceiptService fiskalyReceipts,
        IUserService userService,
        IAuditLogService auditLog,
        ILogger<PosReceiptsController> logger)
    {
        _receiptService = receiptService;
        _paymentService = paymentService;
        _fiskalyReceipts = fiskalyReceipts;
        _userService = userService;
        _auditLog = auditLog;
        _logger = logger;
    }

    /// <summary>
    /// Last receipts for the current cash register (max 20). Tenant isolation via receipt query filters.
    /// Canonical: <c>GET /api/pos/receipts/recent?limit=20</c>. Collection GET remains as an alias.
    /// </summary>
    [HttpGet]
    [HttpGet("recent")]
    [HasPermission(AppPermissions.SaleView)]
    [ProducesResponseType(typeof(PagedResult<ReceiptListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReceiptListItemDto>>> GetRecent(
        [FromQuery] Guid cashRegisterId,
        [FromQuery] int pageSize = MaxRecentPageSize,
        [FromQuery] int limit = 0)
    {
        if (cashRegisterId == Guid.Empty)
            return BadRequest(new { message = "cashRegisterId is required." });

        var requested = limit > 0 ? limit : pageSize;
        var size = Math.Clamp(requested <= 0 ? MaxRecentPageSize : requested, 1, MaxRecentPageSize);
        var result = await _receiptService.GetRecentReceiptsForCashRegisterAsync(cashRegisterId, size);
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

    /// <summary>
    /// POS Belegliste storno. Same Fiskaly CANCELLATION path as FA
    /// (<see cref="IFiskalyReceiptService.CancelReceiptAsync"/>). Cashier: own receipt, Vienna today.
    /// Cross-tenant / wrong register → HTTP 404.
    /// </summary>
    [HttpPost("{receiptId:guid}/cancel")]
    [HasPermission(AppPermissions.PaymentCancel)]
    [ProducesResponseType(typeof(StornoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StornoResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(StornoResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StornoResponse>> Cancel(
        Guid receiptId,
        [FromQuery] Guid cashRegisterId,
        [FromBody] PosReceiptCancelRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            return BadRequest(new StornoResponse { Success = false, ErrorKey = "errors.validationFailed", DiagnosticCode = "CASH_REGISTER_REQUIRED" });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new StornoResponse { Success = false, ErrorKey = "errors.unauthorized" });

        var userRole = User.GetActorRole();
        if (string.IsNullOrWhiteSpace(userRole))
        {
            var user = await _userService.GetUserByIdAsync(userId);
            userRole = user?.Role;
        }

        var receipt = await _receiptService.GetReceiptForCashRegisterAsync(receiptId, cashRegisterId);
        if (receipt == null)
            return NotFound(new StornoResponse { Success = false, ErrorKey = "errors.paymentNotFound", DiagnosticCode = "RECEIPT_NOT_FOUND" });

        if (!string.IsNullOrWhiteSpace(receipt.RksvSpecialReceiptKind))
        {
            return BadRequest(new StornoResponse
            {
                Success = false,
                ErrorKey = "errors.specialReceiptNotStornoable",
                DiagnosticCode = "SPECIAL_RECEIPT",
            });
        }

        if (string.Equals(receipt.FiscalTraceKind, "Storno", StringComparison.OrdinalIgnoreCase)
            || string.Equals(receipt.FiscalTraceKind, "Refund", StringComparison.OrdinalIgnoreCase)
            || receipt.GrandTotal <= 0)
        {
            return BadRequest(new StornoResponse
            {
                Success = false,
                ErrorKey = "errors.alreadyCancelled",
                DiagnosticCode = "STORNO_TARGET_IS_REVERSAL",
            });
        }

        var originalPayment = await _paymentService.GetPaymentAsync(receipt.PaymentId);
        if (originalPayment == null)
        {
            return NotFound(new StornoResponse
            {
                Success = false,
                ErrorKey = "errors.paymentNotFound",
            });
        }

        if (originalPayment.IsStorno || originalPayment.IsRefund)
        {
            return BadRequest(new StornoResponse
            {
                Success = false,
                ErrorKey = "errors.alreadyCancelled",
                DiagnosticCode = originalPayment.IsStorno ? "STORNO_TARGET_IS_STORNO" : "REFUND_ROW_NOT_STORNO_TARGET",
            });
        }

        var gate = EvaluateCashierGate(userRole, userId, originalPayment.CashierId, receipt.Date);
        if (gate != null)
            return gate;

        var reason = PosReceiptStornoEligibility.ResolveReason(request?.Reason);
        if (reason == null)
        {
            return BadRequest(new StornoResponse
            {
                Success = false,
                ErrorKey = "errors.reasonRequired",
                DiagnosticCode = "CANCELLATION_REASON_REQUIRED",
            });
        }

        var result = await _fiskalyReceipts.CancelReceiptAsync(
            cashRegisterId,
            receipt.PaymentId,
            reason,
            userId,
            string.Equals(userRole, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            var diagnostic = result.Error?.Code;
            var status = result.StatusCode == StatusCodes.Status404NotFound
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            var body = new StornoResponse
            {
                Success = false,
                ErrorKey = MapCancelDiagnosticToErrorKey(diagnostic),
                DiagnosticCode = diagnostic,
                RequiresApproval = string.Equals(
                    diagnostic,
                    FiskalyReceiptErrorCodes.ApprovalRequired,
                    StringComparison.Ordinal),
            };
            return StatusCode(status, body);
        }

        Guid? stornoPaymentId = null;
        if (Guid.TryParse(result.Data?.ReceiptId, out var parsedId))
            stornoPaymentId = parsedId;

        await _auditLog.LogPaymentOperationAsync(
            AuditLogActions.PAYMENT_CANCEL,
            AuditLogEntityTypes.PAYMENT,
            stornoPaymentId,
            userId,
            userRole ?? Roles.FallbackUnknown,
            amount: originalPayment.TotalAmount,
            paymentMethod: originalPayment.PaymentMethodRaw,
            description: $"POS Belegliste storno for receipt {originalPayment.ReceiptNumber}",
            requestData: new { receiptId, cashRegisterId },
            responseData: new { stornoPaymentId, originalReceiptNumber = originalPayment.ReceiptNumber });

        return Ok(new StornoResponse
        {
            Success = true,
            StornoPaymentId = stornoPaymentId,
            MessageKey = "messages.stornoSuccess",
        });
    }

    private static ActionResult<StornoResponse>? EvaluateCashierGate(
        string? userRole,
        string userId,
        string? receiptCashierId,
        DateTime issuedAt)
    {
        if (!PosReceiptStornoEligibility.IsCashier(userRole)
            || PosReceiptStornoEligibility.IsPrivilegedPosActor(userRole))
            return null;

        if (!PosReceiptStornoEligibility.IsOwnReceipt(receiptCashierId, userId))
        {
            return new BadRequestObjectResult(new StornoResponse
            {
                Success = false,
                ErrorKey = PosReceiptStornoEligibility.NotOwnerErrorKey,
                DiagnosticCode = PosReceiptStornoEligibility.NotOwnerDiagnostic,
            });
        }

        if (!PosReceiptStornoEligibility.IsViennaCalendarToday(issuedAt))
        {
            return new BadRequestObjectResult(new StornoResponse
            {
                Success = false,
                ErrorKey = PosReceiptStornoEligibility.NotTodayErrorKey,
                DiagnosticCode = PosReceiptStornoEligibility.NotTodayDiagnostic,
            });
        }

        return null;
    }

    private static string MapCancelDiagnosticToErrorKey(string? diagnosticCode) =>
        diagnosticCode switch
        {
            "STORNO_BLOCKED_BY_REFUNDS" => "errors.stornoBlockedByRefunds",
            "CANCELLATION_REASON_REQUIRED" => "errors.reasonRequired",
            "ALREADY_CANCELLED" => "errors.alreadyCancelled",
            FiskalyReceiptErrorCodes.ApprovalRequired => "errors.approvalRequired",
            FiskalyReceiptErrorCodes.PaymentNotFound => "errors.paymentNotFound",
            _ => "errors.stornoFailed",
        };
}
