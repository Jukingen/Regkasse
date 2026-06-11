using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// POS full receipt storno (RKSV reversal). Delegates fiscal work to <see cref="IPaymentService.CancelPaymentAsync"/>;
/// original sale row is never mutated in place.
/// </summary>
[ApiController]
[Route("api/pos/storno")]
[Authorize]
public sealed class PosStornoController : ControllerBase
{
    private const int DefaultStornoWindowHours = 24;

    private readonly AppDbContext _context;
    private readonly IPaymentService _paymentService;
    private readonly IUserService _userService;
    private readonly IAuditLogService _auditLog;

    public PosStornoController(
        AppDbContext context,
        IPaymentService paymentService,
        IUserService userService,
        IAuditLogService auditLog)
    {
        _context = context;
        _paymentService = paymentService;
        _userService = userService;
        _auditLog = auditLog;
    }

    [HttpPost]
    [HasPermission(AppPermissions.PaymentCancel)]
    [ProducesResponseType(typeof(StornoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StornoResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(StornoResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StornoResponse>> StornoPayment(
        [FromBody] StornoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(new StornoResponse { Success = false, ErrorKey = "errors.validationFailed" });

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var userRole = User.GetActorRole();
        if (string.IsNullOrWhiteSpace(userRole))
        {
            var user = await _userService.GetUserByIdAsync(userId);
            userRole = user?.Role;
        }

        var originalPayment = await _paymentService.GetPaymentAsync(request.PaymentId);
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

        var hasStornoChild = await _context.PaymentDetails.AsNoTracking()
            .AnyAsync(p => p.OriginalPaymentId == request.PaymentId && p.IsStorno, cancellationToken);
        if (!originalPayment.IsActive || hasStornoChild)
        {
            return BadRequest(new StornoResponse
            {
                Success = false,
                ErrorKey = "errors.alreadyCancelled",
                DiagnosticCode = "ALREADY_CANCELLED",
            });
        }

        if (!string.IsNullOrWhiteSpace(originalPayment.RksvSpecialReceiptKind))
        {
            return BadRequest(new StornoResponse
            {
                Success = false,
                ErrorKey = "errors.specialReceiptNotStornoable",
                DiagnosticCode = "SPECIAL_RECEIPT",
            });
        }

        var hoursSincePayment = (DateTime.UtcNow - originalPayment.CreatedAt).TotalHours;
        if (hoursSincePayment > DefaultStornoWindowHours
            && !string.Equals(userRole, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new StornoResponse
            {
                Success = false,
                ErrorKey = "errors.stornoTimeLimitExceeded",
                DiagnosticCode = "STORNO_TIME_LIMIT",
            });
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5)
        {
            return BadRequest(new StornoResponse
            {
                Success = false,
                ErrorKey = "errors.reasonRequired",
                DiagnosticCode = "CANCELLATION_REASON_REQUIRED",
            });
        }

        var reasonCode = PosStornoReasonCodeMapper.Map(request.ReasonCode);
        var result = await _paymentService.CancelPaymentAsync(
            request.PaymentId,
            request.Reason.Trim(),
            userId,
            request.IdempotencyKey?.Trim(),
            reasonCode,
            request.ApprovalToken?.Trim());

        if (string.Equals(result.DiagnosticCode, "REVERSAL_APPROVAL_REQUIRED", StringComparison.Ordinal))
        {
            return Ok(new StornoResponse
            {
                Success = false,
                ErrorKey = "errors.approvalRequired",
                RequiresApproval = true,
                ApprovalRequestId = result.ApprovalRequestId,
                ApprovalTokenExpiresAtUtc = result.ApprovalTokenExpiresAtUtc,
                DiagnosticCode = result.DiagnosticCode,
            });
        }

        if (!result.Success)
        {
            return BadRequest(new StornoResponse
            {
                Success = false,
                ErrorKey = MapDiagnosticToErrorKey(result.DiagnosticCode),
                DiagnosticCode = result.DiagnosticCode,
            });
        }

        var stornoPaymentId = result.Payment?.Id ?? result.PaymentId;
        await _auditLog.LogPaymentOperationAsync(
            AuditLogActions.PAYMENT_CANCEL,
            AuditLogEntityTypes.PAYMENT,
            stornoPaymentId,
            userId,
            userRole ?? Roles.FallbackUnknown,
            amount: originalPayment.TotalAmount,
            paymentMethod: originalPayment.PaymentMethodRaw,
            description: $"POS storno for receipt {originalPayment.ReceiptNumber}",
            requestData: new { request.PaymentId, request.ReasonCode },
            responseData: new { stornoPaymentId, originalReceiptNumber = originalPayment.ReceiptNumber });

        return Ok(new StornoResponse
        {
            Success = true,
            StornoPaymentId = stornoPaymentId,
            MessageKey = "messages.stornoSuccess",
        });
    }

    private static string MapDiagnosticToErrorKey(string? diagnosticCode) =>
        diagnosticCode switch
        {
            "STORNO_BLOCKED_BY_REFUNDS" => "errors.stornoBlockedByRefunds",
            "CANCELLATION_REASON_REQUIRED" => "errors.reasonRequired",
            _ => "errors.stornoFailed",
        };
}
