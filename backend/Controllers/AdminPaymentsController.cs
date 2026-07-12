using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin canonical payments surface (source of truth for payment rows, receipts linkage, and admin actions).
/// <c>FinanzOnline*</c> columns on payments are <b>derived</b> from submit/retry responses for UX and legacy reconciliation — for BMF pipeline state prefer
/// <see cref="FinanzOnlineOutboxAdminController"/> (filter by correlation id / business key / aggregate).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/payments")]
[Produces("application/json")]
[HasPermission(AppPermissions.PaymentView)]
public class AdminPaymentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPaymentService _paymentService;
    private readonly IReceiptPdfService _receiptPdfService;
    private readonly IAdminPaymentListService _paymentListService;
    private readonly IAdminSuspiciousAlertService _suspiciousAlertService;
    private readonly IPaymentTrendAnalysisService _trendAnalysisService;
    private readonly IPaymentReversalApprovalService _reversalApproval;
    private readonly IOptionsMonitor<PaymentReversalApprovalOptions> _reversalOptions;
    private readonly ILogger<AdminPaymentsController> _logger;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public AdminPaymentsController(
        AppDbContext context,
        IPaymentService paymentService,
        IReceiptPdfService receiptPdfService,
        IAdminPaymentListService paymentListService,
        IAdminSuspiciousAlertService suspiciousAlertService,
        IPaymentTrendAnalysisService trendAnalysisService,
        IPaymentReversalApprovalService reversalApproval,
        IOptionsMonitor<PaymentReversalApprovalOptions> reversalOptions,
        ILogger<AdminPaymentsController> logger,
        ISettingsTenantResolver settingsTenantResolver)
    {
        _context = context;
        _paymentService = paymentService;
        _receiptPdfService = receiptPdfService;
        _paymentListService = paymentListService;
        _suspiciousAlertService = suspiciousAlertService;
        _trendAnalysisService = trendAnalysisService;
        _reversalApproval = reversalApproval;
        _reversalOptions = reversalOptions;
        _logger = logger;
        _settingsTenantResolver = settingsTenantResolver;
    }

    [HttpGet]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<PaymentListResponse>> GetPayments(
        [FromQuery] PaymentFilterDto filter,
        [FromQuery] string? method = null,
        [FromQuery] string? status = null,
        [FromQuery] int? pageNumber = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? stornoReason = null,
        CancellationToken cancellationToken = default)
    {
        MergeLegacyListParams(filter, method, status, pageNumber, pageSize);

        try
        {
            var (response, errorCode, errorMessage) =
                await _paymentListService.QueryAsync(filter, stornoReason, cancellationToken);

            if (errorCode != null)
                return BadRequest(new { message = errorMessage, code = errorCode });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin payments list failed");
            return StatusCode(500, new { message = "Failed to retrieve admin payments list", code = "ADMIN_PAYMENTS_LIST_ERROR" });
        }
    }

    private static int GetActiveFilterCount(PaymentFilterDto filter)
    {
        var count = 0;
        if (filter.StartDate.HasValue) count++;
        if (filter.EndDate.HasValue) count++;
        if (filter.MinAmount.HasValue) count++;
        if (filter.MaxAmount.HasValue) count++;
        if (filter.PaymentMethods.Count > 0) count++;
        if (filter.Statuses.Count > 0) count++;
        if (filter.CashRegisterId.HasValue) count++;
        if (!string.IsNullOrEmpty(filter.CustomerName)) count++;
        if (!string.IsNullOrEmpty(filter.CustomerEmail)) count++;
        if (!string.IsNullOrEmpty(filter.CashierId)) count++;
        if (!string.IsNullOrEmpty(filter.ReceiptNumber)) count++;
        if (filter.IsStorno.HasValue) count++;
        if (filter.IsRefund.HasValue) count++;
        return count;
    }

    private static Dictionary<string, object> GetAppliedFilters(PaymentFilterDto filter)
    {
        var applied = new Dictionary<string, object>();

        if (filter.StartDate.HasValue)
            applied["startDate"] = filter.StartDate.Value;
        if (filter.EndDate.HasValue)
            applied["endDate"] = filter.EndDate.Value;
        if (filter.MinAmount.HasValue)
            applied["minAmount"] = filter.MinAmount.Value;
        if (filter.MaxAmount.HasValue)
            applied["maxAmount"] = filter.MaxAmount.Value;
        if (filter.PaymentMethods.Count > 0)
            applied["paymentMethods"] = filter.PaymentMethods;
        if (filter.Statuses.Count > 0)
            applied["statuses"] = filter.Statuses;
        if (filter.CashRegisterId.HasValue)
            applied["cashRegisterId"] = filter.CashRegisterId.Value;
        if (!string.IsNullOrEmpty(filter.CustomerName))
            applied["customerName"] = filter.CustomerName;
        if (!string.IsNullOrEmpty(filter.CustomerEmail))
            applied["customerEmail"] = filter.CustomerEmail;
        if (!string.IsNullOrEmpty(filter.CashierId))
            applied["cashierId"] = filter.CashierId;
        if (!string.IsNullOrEmpty(filter.ReceiptNumber))
            applied["receiptNumber"] = filter.ReceiptNumber;
        if (filter.IsStorno.HasValue)
            applied["isStorno"] = filter.IsStorno.Value;
        if (filter.IsRefund.HasValue)
            applied["isRefund"] = filter.IsRefund.Value;

        return applied;
    }

    private static void MergeLegacyListParams(
        PaymentFilterDto filter,
        string? method,
        string? status,
        int? pageNumber,
        int? pageSize)
    {
        if (!string.IsNullOrWhiteSpace(method) && filter.PaymentMethods.Count == 0)
            filter.PaymentMethods.Add(method.Trim());

        if (!string.IsNullOrWhiteSpace(status) && filter.Statuses.Count == 0)
            filter.Statuses.Add(status.Trim());

        if (pageNumber.HasValue && pageNumber.Value > 0)
            filter.Page = pageNumber.Value;

        if (pageSize.HasValue && pageSize.Value > 0)
            filter.PageSize = pageSize.Value;
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<AdminPaymentDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var p = await _context.PaymentDetails.AsNoTracking()
                .Where(x => x.Id == id)
                .Where(x => _context.CashRegisters.ForResolvedTenantScope().Any(cr => cr.Id == x.CashRegisterId && cr.TenantId == tenantId))
                .FirstOrDefaultAsync(cancellationToken);
            if (p == null)
                return NotFound(new { message = "Payment not found", code = "ADMIN_PAYMENT_NOT_FOUND" });

            var receipt = await _context.Receipts.AsNoTracking().FirstOrDefaultAsync(r => r.PaymentId == id, cancellationToken);
            var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.SourcePaymentId == id, cancellationToken);
            var voucherRedeemedAmount = await _context.VoucherLedgerEntries.AsNoTracking()
                .Where(l => l.PaymentId == id && l.Type == VoucherTransactionType.Redeem)
                .Select(l => (decimal?)(-l.Amount))
                .SumAsync(cancellationToken) ?? 0m;

            var cashierDisplay = await _context.Users.AsNoTracking()
                .Where(u => u.Id == p.CashierId)
                .Select(u => u.FirstName + " " + u.LastName)
                .FirstOrDefaultAsync(cancellationToken);

            var stornoRefundAudit = await BuildStornoRefundAuditSectionAsync(p, tenantId, cancellationToken);

            Guid? stornoReversalPaymentId = null;
            string? stornoReversalReceiptNumber = null;
            if (!p.IsStorno && !p.IsRefund)
            {
                var stornoChild = await _context.PaymentDetails.AsNoTracking()
                    .Where(x => x.OriginalPaymentId == id && x.IsStorno)
                    .Select(x => new { x.Id, x.ReceiptNumber })
                    .FirstOrDefaultAsync(cancellationToken);
                if (stornoChild != null)
                {
                    stornoReversalPaymentId = stornoChild.Id;
                    stornoReversalReceiptNumber = stornoChild.ReceiptNumber;
                }
            }

            return Ok(new AdminPaymentDetailDto
            {
                Id = p.Id,
                TransactionId = p.TransactionId,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                TotalAmount = p.TotalAmount,
                TaxAmount = p.TaxAmount,
                Currency = "EUR",
                Method = ParsePaymentMethodName(p.PaymentMethodRaw),
                PaymentMethodRaw = p.PaymentMethodRaw,
                Status = ResolvePaymentStatus(p),
                CustomerId = p.CustomerId,
                CustomerName = p.CustomerName,
                CashierId = p.CashierId,
                CashierDisplayName = string.IsNullOrWhiteSpace(cashierDisplay?.Trim()) ? p.CashierId : cashierDisplay!.Trim(),
                CashRegisterId = p.CashRegisterId,
                TableNumber = p.TableNumber,
                ReceiptNumber = p.ReceiptNumber,
                ReceiptId = receipt?.ReceiptId,
                InvoiceId = invoice?.Id,
                InvoiceNumber = invoice?.InvoiceNumber,
                IsActive = p.IsActive,
                IsRefund = p.IsRefund,
                IsStorno = p.IsStorno,
                StornoReason = p.StornoReason,
                OriginalPaymentId = p.OriginalPaymentId,
                OriginalReceiptId = p.OriginalReceiptId,
                RefundReason = p.RefundReason,
                RefundAmount = p.RefundAmount,
                RefundedAt = p.RefundedAt,
                CancellationReason = p.CancellationReason,
                CancelledAt = p.CancelledAt,
                OfflineTransactionId = p.OfflineTransactionId,
                OfflineReplayBatchCorrelationId = p.OfflineReplayBatchCorrelationId,
                IsOfflineOrigin = p.OfflineTransactionId != null,
                IdempotencyKey = p.IdempotencyKey,
                CancelIdempotencyKey = p.CancelIdempotencyKey,
                FinanzOnlineStatus = p.FinanzOnlineStatus,
                FinanzOnlineError = p.FinanzOnlineError,
                FinanzOnlineReferenceId = p.FinanzOnlineReferenceId,
                FinanzOnlineLastAttemptAtUtc = p.FinanzOnlineLastAttemptAtUtc,
                FinanzOnlineRetryCount = p.FinanzOnlineRetryCount,
                InvoicePersisted = invoice != null,
                VoucherRedeemedAmount = voucherRedeemedAmount,
                SettlementAmount = decimal.Round(p.TotalAmount - voucherRedeemedAmount, 2, MidpointRounding.AwayFromZero),
                HasVoucherRedemption = voucherRedeemedAmount > 0m,
                StornoRefundAudit = stornoRefundAudit,
                HasStornoReversal = stornoReversalPaymentId.HasValue,
                StornoReversalPaymentId = stornoReversalPaymentId,
                StornoReversalReceiptNumber = stornoReversalReceiptNumber,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin payment detail failed for PaymentId={PaymentId}", id);
            return StatusCode(500, new { message = "Failed to retrieve payment detail", code = "ADMIN_PAYMENT_DETAIL_ERROR" });
        }
    }

    /// <summary>
    /// RKSV-compliant receipt reprint as PDF (watermarked). Uses persisted receipt/QR/TSE snapshot only; no new signing or DB rows.
    /// </summary>
    [HttpGet("{id:guid}/reprint-pdf")]
    [HasPermission(AppPermissions.ReceiptReprint)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadReprintPdf(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await _receiptPdfService.GenerateReprintPdfAsync(id, cancellationToken).ConfigureAwait(false);
            var fileName = $"Beleg-Nachdruck-{id:N}.pdf";
            return File(bytes, "application/pdf", fileName);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogInformation(ex, "Admin receipt reprint PDF: not found PaymentId={PaymentId}", id);
            return NotFound(new { message = "Receipt not found for this payment", code = "ADMIN_PAYMENT_REPRINT_NOT_FOUND" });
        }
    }

    /// <summary>
    /// Same RKSV-safe reprint PDF as <see cref="DownloadReprintPdf"/>; alternate path and filename for integrations.
    /// </summary>
    [HttpGet("{paymentId:guid}/reprint")]
    [HasPermission(AppPermissions.ReceiptReprint)]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ReprintReceipt(Guid paymentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var pdfBytes = await _receiptPdfService.GenerateReprintPdfAsync(paymentId, cancellationToken).ConfigureAwait(false);
            var fileName = $"beleg_{paymentId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Payment not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin receipt reprint failed PaymentId={PaymentId}", paymentId);
            return StatusCode(500, new { error = "Failed to generate reprint", details = ex.Message });
        }
    }

    [HttpGet("statistics")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<PaymentStatistics>> GetStatistics(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (startDate > endDate)
            return BadRequest(new { message = "startDate must be <= endDate", code = "ADMIN_PAYMENTS_INVALID_RANGE" });

        try
        {
            var stats = await _paymentService.GetPaymentStatisticsAsync(startDate, endDate);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin payments statistics failed");
            return StatusCode(500, new { message = "Failed to retrieve payment statistics", code = "ADMIN_PAYMENTS_STATS_ERROR" });
        }
    }

    /// <summary>List suspicious transaction alerts for the effective tenant.</summary>
    [HttpGet("alerts")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<SuspiciousAlertsListResponseDto>> GetAlerts(
        [FromQuery] bool unreadOnly = true,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var result = await _suspiciousAlertService.ListAsync(tenantId, unreadOnly, cancellationToken);
        return Ok(result);
    }

    /// <summary>Mark a suspicious transaction alert as read (acknowledged).</summary>
    [HttpPost("alerts/{id:guid}/read")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<IActionResult> MarkAlertAsRead(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var actorUserId = User.GetActorUserId();
        var found = await _suspiciousAlertService.MarkAsReadAsync(tenantId, id, actorUserId, cancellationToken);
        if (!found)
            return NotFound(new { message = "Alert not found", code = "ADMIN_PAYMENTS_ALERT_NOT_FOUND" });

        return Ok(new { success = true });
    }

    /// <summary>Payment trend analysis (daily / weekly / monthly buckets with period comparison).</summary>
    [HttpGet("trends")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<TrendAnalysisResponse>> GetPaymentTrends(
        [FromQuery] TrendPeriod period = TrendPeriod.Daily,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
            return BadRequest(new { message = "startDate must be <= endDate", code = "ADMIN_PAYMENTS_INVALID_RANGE" });

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var result = await _trendAnalysisService.GetTrendAnalysisAsync(
            tenantId,
            period,
            startDate,
            endDate,
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(AppPermissions.PaymentCancel)]
    public async Task<ActionResult<CancellationResponse>> CancelPayment(
        Guid id,
        [FromBody] CancelPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated", code = "UNAUTHORIZED" });

        try
        {
            var payment = await _paymentService.GetPaymentAsync(id);
            if (payment == null || !await PaymentBelongsToEffectiveTenantAsync(payment, cancellationToken))
                return NotFound(new { message = "Payment not found", code = "ADMIN_PAYMENTS_NOT_FOUND" });

            if (payment.IsStorno)
            {
                return BadRequest(new CancellationResponse
                {
                    Success = false,
                    Message = "Payment already cancelled",
                    DiagnosticCode = "ALREADY_STORNO",
                });
            }

            var alreadyReversed = await _context.PaymentDetails.AsNoTracking()
                .AnyAsync(p => p.OriginalPaymentId == id && p.IsStorno, cancellationToken);
            if (alreadyReversed)
            {
                return BadRequest(new CancellationResponse
                {
                    Success = false,
                    Message = "Payment already cancelled",
                    DiagnosticCode = "ALREADY_CANCELLED",
                });
            }

            PaymentReversalPolicyDto? policy = null;
            if (string.IsNullOrWhiteSpace(request.ApprovalToken))
            {
                policy = await _reversalApproval.AssessPolicyAsync(
                    payment,
                    PaymentReversalOperation.Cancel,
                    null,
                    userId,
                    cancellationToken);
            }

            var result = await _paymentService.CancelPaymentAsync(
                id,
                request.Reason,
                userId,
                request.IdempotencyKey?.Trim(),
                request.ReasonCode,
                request.ApprovalToken?.Trim());

            if (string.Equals(result.DiagnosticCode, "REVERSAL_APPROVAL_REQUIRED", StringComparison.Ordinal))
            {
                policy ??= await _reversalApproval.AssessPolicyAsync(
                    payment,
                    PaymentReversalOperation.Cancel,
                    null,
                    userId,
                    cancellationToken);
                return Ok(MapCancellationResponse(result, policy));
            }

            if (!result.Success)
                return BadRequest(MapCancellationResponse(result, policy));

            return Ok(MapCancellationResponse(result, policy));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin cancel payment failed for PaymentId={PaymentId}", id);
            return StatusCode(500, new { message = "Cancel operation failed", code = "ADMIN_PAYMENTS_CANCEL_ERROR" });
        }
    }

    [HttpPost("{id:guid}/refund")]
    [HasPermission(AppPermissions.RefundCreate)]
    public async Task<ActionResult<RefundResponse>> RefundPayment(
        Guid id,
        [FromBody] RefundPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated", code = "UNAUTHORIZED" });

        try
        {
            var payment = await _paymentService.GetPaymentAsync(id);
            if (payment == null || !await PaymentBelongsToEffectiveTenantAsync(payment, cancellationToken))
                return NotFound(new { message = "Payment not found", code = "ADMIN_PAYMENTS_NOT_FOUND" });

            PaymentReversalPolicyDto? policy = null;
            if (string.IsNullOrWhiteSpace(request.ApprovalToken))
            {
                policy = await _reversalApproval.AssessPolicyAsync(
                    payment,
                    PaymentReversalOperation.Refund,
                    request.Amount,
                    userId,
                    cancellationToken);
            }

            var result = await _paymentService.RefundPaymentAsync(
                id,
                request.Amount,
                request.Reason,
                userId,
                request.IdempotencyKey?.Trim(),
                request.ReasonCode,
                request.ApprovalToken?.Trim());

            if (string.Equals(result.DiagnosticCode, "REVERSAL_APPROVAL_REQUIRED", StringComparison.Ordinal))
            {
                policy ??= await _reversalApproval.AssessPolicyAsync(
                    payment,
                    PaymentReversalOperation.Refund,
                    request.Amount,
                    userId,
                    cancellationToken);
                return Ok(MapRefundResponse(result, policy));
            }

            if (!result.Success)
                return BadRequest(MapRefundResponse(result, policy));

            return Ok(MapRefundResponse(result, policy));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin refund payment failed for PaymentId={PaymentId}", id);
            return StatusCode(500, new { message = "Refund operation failed", code = "ADMIN_PAYMENTS_REFUND_ERROR" });
        }
    }

    [HttpGet("{id:guid}/reversal-policy")]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<PaymentReversalPolicyDto>> GetReversalPolicy(
        Guid id,
        [FromQuery] PaymentReversalOperation operation = PaymentReversalOperation.Cancel,
        [FromQuery] decimal? refundAmount = null,
        CancellationToken cancellationToken = default)
    {
        var payment = await _paymentService.GetPaymentAsync(id);
        if (payment == null)
            return NotFound(new { message = "Payment not found", code = "ADMIN_PAYMENTS_NOT_FOUND" });

        if (!await PaymentBelongsToEffectiveTenantAsync(payment, cancellationToken))
            return NotFound(new { message = "Payment not found", code = "ADMIN_PAYMENTS_NOT_FOUND" });

        return Ok(await _reversalApproval.AssessPolicyAsync(
            payment,
            operation,
            refundAmount,
            User.GetActorUserId(),
            cancellationToken));
    }

    private async Task<bool> PaymentBelongsToEffectiveTenantAsync(
        PaymentDetails payment,
        CancellationToken cancellationToken)
    {
        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        return await _context.CashRegisters.AsNoTracking().ForResolvedTenantScope()
            .AnyAsync(cr => cr.Id == payment.CashRegisterId && cr.TenantId == tenantId, cancellationToken);
    }

    private CancellationResponse MapCancellationResponse(
        PaymentResult result,
        PaymentReversalPolicyDto? policy = null)
    {
        var pendingApproval = string.Equals(
            result.DiagnosticCode,
            "REVERSAL_APPROVAL_REQUIRED",
            StringComparison.Ordinal);
        var defaultWait = Math.Max(60, _reversalOptions.CurrentValue.ApprovalTokenTtlMinutes * 60);
        var waitSeconds = result.ApprovalTokenExpiresAtUtc.HasValue
            ? (int)Math.Max(0, (result.ApprovalTokenExpiresAtUtc.Value - DateTime.UtcNow).TotalSeconds)
            : defaultWait;

        var primaryReason = policy?.Reason;
        var message = pendingApproval && !string.IsNullOrWhiteSpace(primaryReason)
            ? $"This cancellation requires manager approval. Reason: {primaryReason}"
            : result.Message;

        return new CancellationResponse
        {
            Success = result.Success && !pendingApproval,
            RequiresApproval = result.RequiresApproval || pendingApproval,
            ApprovalId = result.ApprovalRequestId,
            Message = message,
            WaitTimeSeconds = pendingApproval ? waitSeconds : null,
            CancelledAt = result.Success && !pendingApproval
                ? result.Payment?.CancelledAt ?? DateTime.UtcNow
                : null,
            PaymentId = result.Payment?.Id,
            DiagnosticCode = result.DiagnosticCode,
            Errors = result.Errors,
            ApprovalNotificationSent = result.ApprovalNotificationSent,
            Reasons = policy?.Reasons ?? Array.Empty<string>(),
        };
    }

    private RefundResponse MapRefundResponse(PaymentResult result, PaymentReversalPolicyDto? policy = null)
    {
        var pendingApproval = string.Equals(
            result.DiagnosticCode,
            "REVERSAL_APPROVAL_REQUIRED",
            StringComparison.Ordinal);
        var defaultWait = Math.Max(60, _reversalOptions.CurrentValue.ApprovalTokenTtlMinutes * 60);
        var waitSeconds = result.ApprovalTokenExpiresAtUtc.HasValue
            ? (int)Math.Max(0, (result.ApprovalTokenExpiresAtUtc.Value - DateTime.UtcNow).TotalSeconds)
            : defaultWait;

        var primaryReason = policy?.Reason;
        var message = pendingApproval && !string.IsNullOrWhiteSpace(primaryReason)
            ? $"This refund requires manager approval. Reason: {primaryReason}"
            : result.Message;

        return new RefundResponse
        {
            Success = result.Success && !pendingApproval,
            RequiresApproval = result.RequiresApproval || pendingApproval,
            ApprovalId = result.ApprovalRequestId,
            Message = message,
            WaitTimeSeconds = pendingApproval ? waitSeconds : null,
            RefundedAt = result.Success && !pendingApproval
                ? result.Payment?.RefundedAt ?? DateTime.UtcNow
                : null,
            PaymentId = result.Payment?.Id,
            DiagnosticCode = result.DiagnosticCode,
            Errors = result.Errors,
            ApprovalNotificationSent = result.ApprovalNotificationSent,
            Reasons = policy?.Reasons ?? Array.Empty<string>(),
        };
    }

    private async Task<AdminPaymentStornoRefundAuditDto?> BuildStornoRefundAuditSectionAsync(
        PaymentDetails reversal,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!reversal.IsStorno && !reversal.IsRefund)
            return null;

        PaymentDetails? original = null;
        if (reversal.OriginalPaymentId.HasValue)
        {
            original = await _context.PaymentDetails.AsNoTracking()
                .Where(x => x.Id == reversal.OriginalPaymentId.Value)
                .Where(x => _context.CashRegisters.ForResolvedTenantScope().Any(cr => cr.Id == x.CashRegisterId && cr.TenantId == tenantId))
                .FirstOrDefaultAsync(cancellationToken);
        }

        var originalLines = ParseAuditLineItems(original?.PaymentItems);
        var reversalLines = ParseAuditLineItems(reversal.PaymentItems);

        double? deltaSeconds = original != null
            ? (reversal.CreatedAt - original.CreatedAt).TotalSeconds
            : null;

        var entityIds = new List<Guid> { reversal.Id };
        if (reversal.OriginalPaymentId.HasValue)
            entityIds.Add(reversal.OriginalPaymentId.Value);

        var relatedAuditEvents = await _context.AuditLogs.AsNoTracking()
            .Where(a => a.EntityId != null && entityIds.Contains(a.EntityId.Value))
            .OrderByDescending(a => a.Timestamp)
            .Take(80)
            .Select(a => new AdminPaymentAuditEventDto
            {
                TimestampUtc = a.Timestamp,
                Action = a.Action,
                UserId = a.UserId,
                UserRole = a.UserRole,
                Description = a.Description,
                HttpStatusCode = a.HttpStatusCode
            })
            .ToListAsync(cancellationToken);

        return new AdminPaymentStornoRefundAuditDto
        {
            OriginalPaymentId = original?.Id,
            OriginalReceiptNumber = original?.ReceiptNumber,
            OriginalCreatedAtUtc = original?.CreatedAt,
            OriginalTotalAmount = original?.TotalAmount,
            OriginalLineItems = originalLines,
            ReversalLineItems = reversalLines,
            SecondsBetweenOriginalAndReversal = deltaSeconds,
            RelatedAuditEvents = relatedAuditEvents
        };
    }

    private static IReadOnlyList<AdminPaymentAuditLineItemDto> ParseAuditLineItems(JsonDocument? doc)
    {
        if (doc == null)
            return Array.Empty<AdminPaymentAuditLineItemDto>();
        try
        {
            var items = JsonSerializer.Deserialize<List<PaymentItem>>(doc.RootElement.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (items == null || items.Count == 0)
                return Array.Empty<AdminPaymentAuditLineItemDto>();
            return items.Select(i => new AdminPaymentAuditLineItemDto
            {
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                TaxAmount = i.TaxAmount
            }).ToList();
        }
        catch
        {
            return Array.Empty<AdminPaymentAuditLineItemDto>();
        }
    }

    private static string ResolvePaymentStatus(PaymentDetails p) =>
        AdminPaymentListMapper.ResolvePaymentStatus(p);

    private static string ParsePaymentMethodName(string rawValue) =>
        AdminPaymentListMapper.ParsePaymentMethodName(rawValue);
}

public class AdminPaymentDetailDto
{
    public Guid Id { get; set; }
    public string? TransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Method { get; set; } = "Unknown";
    public string? PaymentMethodRaw { get; set; }
    public string Status { get; set; } = "Success";
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CashierId { get; set; }
    /// <summary>Resolved cashier display name for admin UX (falls back to <see cref="CashierId"/>).</summary>
    public string? CashierDisplayName { get; set; }
    public Guid CashRegisterId { get; set; }
    public int TableNumber { get; set; }
    public string? ReceiptNumber { get; set; }
    public Guid? ReceiptId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public bool IsActive { get; set; }
    public bool IsRefund { get; set; }
    public bool IsStorno { get; set; }
    public StornoReason? StornoReason { get; set; }
    public Guid? OriginalPaymentId { get; set; }
    public Guid? OriginalReceiptId { get; set; }
    public string? RefundReason { get; set; }
    public decimal? RefundAmount { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? OfflineTransactionId { get; set; }
    public Guid? OfflineReplayBatchCorrelationId { get; set; }
    public bool IsOfflineOrigin { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? CancelIdempotencyKey { get; set; }
    public string? FinanzOnlineStatus { get; set; }
    public string? FinanzOnlineError { get; set; }
    public string? FinanzOnlineReferenceId { get; set; }
    public DateTime? FinanzOnlineLastAttemptAtUtc { get; set; }
    public int FinanzOnlineRetryCount { get; set; }
    public bool InvoicePersisted { get; set; }
    public decimal VoucherRedeemedAmount { get; set; }
    public decimal SettlementAmount { get; set; }
    public bool HasVoucherRedemption { get; set; }
    public AdminPaymentStornoRefundAuditDto? StornoRefundAudit { get; set; }
    /// <summary>True when a fiscal storno reversal row exists for this original sale.</summary>
    public bool HasStornoReversal { get; set; }
    public Guid? StornoReversalPaymentId { get; set; }
    public string? StornoReversalReceiptNumber { get; set; }
}

public class AdminPaymentActionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? PaymentId { get; set; }
    public string? DiagnosticCode { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    public bool RequiresApproval { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public DateTime? ApprovalTokenExpiresAtUtc { get; set; }
    public bool ApprovalNotificationSent { get; set; }
}
