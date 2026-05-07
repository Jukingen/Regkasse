using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    private readonly ILogger<AdminPaymentsController> _logger;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public AdminPaymentsController(
        AppDbContext context,
        IPaymentService paymentService,
        ILogger<AdminPaymentsController> logger,
        ISettingsTenantResolver settingsTenantResolver)
    {
        _context = context;
        _paymentService = paymentService;
        _logger = logger;
        _settingsTenantResolver = settingsTenantResolver;
    }

    [HttpGet]
    [HasPermission(AppPermissions.PaymentView)]
    public async Task<ActionResult<AdminPaymentsListResponse>> GetList(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? method = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] bool? isStorno = null,
        [FromQuery] bool? isRefund = null,
        [FromQuery] string? stornoReason = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            IQueryable<PaymentDetails> query = _context.PaymentDetails.AsNoTracking()
                .Where(p => _context.CashRegisters.Any(cr => cr.Id == p.CashRegisterId && cr.TenantId == tenantId));

            if (!startDate.HasValue && !endDate.HasValue)
            {
                // Default list: rolling UTC window; inclusive instant upper bound at request time (not calendar-day semantics).
                var fromUtc = nowUtc.AddDays(-30);
                query = query.Where(p => p.CreatedAt >= fromUtc && p.CreatedAt <= nowUtc);
            }
            else
            {
                var startCal = startDate ?? endDate!.Value;
                var endCal = endDate ?? startDate!.Value;
                if (startCal > endCal)
                    return BadRequest(new { message = "startDate must be <= endDate", code = "ADMIN_PAYMENTS_INVALID_RANGE" });

                var (fromUtc, toExclusiveUtc) =
                    PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(startCal, endCal);
                query = query.Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusiveUtc);
            }

            if (!string.IsNullOrWhiteSpace(method))
            {
                var normalizedMethod = method.Trim();
                query = query.Where(p => ParsePaymentMethodName(p.PaymentMethodRaw) == normalizedMethod);
            }

            if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
            {
                var regOk = await _context.CashRegisters.AsNoTracking()
                    .AnyAsync(cr => cr.Id == cashRegisterId.Value && cr.TenantId == tenantId, cancellationToken);
                if (!regOk)
                    return BadRequest(new { message = "Cash register is not in the current tenant", code = "ADMIN_PAYMENTS_INVALID_REGISTER" });
                query = query.Where(p => p.CashRegisterId == cashRegisterId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim();
                query = query.Where(p => ResolvePaymentStatus(p) == normalizedStatus);
            }

            // Storno/refund audit filters: both true => OR (all reversal rows). Single flag => that type only.
            if (isStorno == true && isRefund == true)
                query = query.Where(p => p.IsStorno || p.IsRefund);
            else if (isStorno == true)
                query = query.Where(p => p.IsStorno);
            else if (isRefund == true)
                query = query.Where(p => p.IsRefund);

            if (!string.IsNullOrWhiteSpace(stornoReason) &&
                Enum.TryParse<StornoReason>(stornoReason.Trim(), ignoreCase: true, out var reasonEnum))
            {
                query = query.Where(p => p.IsStorno && p.StornoReason == reasonEnum);
            }

            var total = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new AdminPaymentListItemDto
                {
                    Id = p.Id,
                    TransactionId = p.TransactionId,
                    CreatedAt = p.CreatedAt,
                    TotalAmount = p.TotalAmount,
                    Currency = "EUR",
                    Method = ParsePaymentMethodName(p.PaymentMethodRaw),
                    Status = ResolvePaymentStatus(p),
                    CustomerName = p.CustomerName,
                    CashRegisterId = p.CashRegisterId,
                    ReceiptNumber = p.ReceiptNumber,
                    ReceiptId = _context.Receipts
                        .Where(r => r.PaymentId == p.Id)
                        .Select(r => (Guid?)r.ReceiptId)
                        .FirstOrDefault(),
                    InvoiceId = _context.Invoices
                        .Where(i => i.SourcePaymentId == p.Id)
                        .Select(i => (Guid?)i.Id)
                        .FirstOrDefault(),
                    InvoiceNumber = _context.Invoices
                        .Where(i => i.SourcePaymentId == p.Id)
                        .Select(i => i.InvoiceNumber)
                        .FirstOrDefault(),
                    IsOfflineOrigin = p.OfflineTransactionId != null,
                    OfflineTransactionId = p.OfflineTransactionId,
                    OfflineReplayBatchCorrelationId = p.OfflineReplayBatchCorrelationId,
                    FinanzOnlineStatus = p.FinanzOnlineStatus,
                    FinanzOnlineError = p.FinanzOnlineError,
                    FinanzOnlineReferenceId = p.FinanzOnlineReferenceId,
                    FinanzOnlineLastAttemptAtUtc = p.FinanzOnlineLastAttemptAtUtc,
                    FinanzOnlineRetryCount = p.FinanzOnlineRetryCount,
                    InvoicePersisted = _context.Invoices.Any(i => i.SourcePaymentId == p.Id),
                    VoucherRedeemedAmount = _context.VoucherLedgerEntries
                        .Where(l => l.PaymentId == p.Id && l.Type == VoucherTransactionType.Redeem)
                        .Select(l => (decimal?)(-l.Amount))
                        .Sum() ?? 0m,
                    HasVoucherRedemption = _context.VoucherLedgerEntries
                        .Any(l => l.PaymentId == p.Id && l.Type == VoucherTransactionType.Redeem),
                    IsStorno = p.IsStorno,
                    IsRefund = p.IsRefund,
                    StornoReason = p.StornoReason,
                    OriginalPaymentId = p.OriginalPaymentId,
                    OriginalReceiptNumber = _context.PaymentDetails
                        .Where(op => op.Id == p.OriginalPaymentId)
                        .Select(op => op.ReceiptNumber)
                        .FirstOrDefault(),
                    CashierDisplayName = _context.Users
                        .Where(u => u.Id == p.CashierId)
                        .Select(u => u.FirstName + " " + u.LastName)
                        .FirstOrDefault(),
                    ReversalCompletionStatus =
                        p.FinanzOnlineStatus != null &&
                        p.FinanzOnlineStatus.ToLower() == "failed"
                            ? "Failed"
                            : "Completed"
                })
                .ToListAsync(cancellationToken);

            return Ok(new AdminPaymentsListResponse
            {
                Total = total,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Items = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin payments list failed");
            return StatusCode(500, new { message = "Failed to retrieve admin payments list", code = "ADMIN_PAYMENTS_LIST_ERROR" });
        }
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
                .Where(x => _context.CashRegisters.Any(cr => cr.Id == x.CashRegisterId && cr.TenantId == tenantId))
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
                StornoRefundAudit = stornoRefundAudit
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin payment detail failed for PaymentId={PaymentId}", id);
            return StatusCode(500, new { message = "Failed to retrieve payment detail", code = "ADMIN_PAYMENT_DETAIL_ERROR" });
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

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(AppPermissions.PaymentCancel)]
    public async Task<ActionResult<AdminPaymentActionResponse>> Cancel(Guid id, [FromBody] CancelPaymentRequest request)
    {
        var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated", code = "UNAUTHORIZED" });

        try
        {
            var result = await _paymentService.CancelPaymentAsync(id, request.Reason, userId, request.IdempotencyKey?.Trim());
            if (!result.Success)
                return BadRequest(new AdminPaymentActionResponse
                {
                    Success = false,
                    Message = result.Message,
                    Errors = result.Errors,
                    DiagnosticCode = result.DiagnosticCode
                });

            return Ok(new AdminPaymentActionResponse
            {
                Success = true,
                Message = result.Message,
                PaymentId = result.Payment?.Id,
                DiagnosticCode = result.DiagnosticCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin cancel payment failed for PaymentId={PaymentId}", id);
            return StatusCode(500, new { message = "Cancel operation failed", code = "ADMIN_PAYMENTS_CANCEL_ERROR" });
        }
    }

    [HttpPost("{id:guid}/refund")]
    [HasPermission(AppPermissions.RefundCreate)]
    public async Task<ActionResult<AdminPaymentActionResponse>> Refund(Guid id, [FromBody] RefundPaymentRequest request)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "User not authenticated", code = "UNAUTHORIZED" });

        try
        {
            var result = await _paymentService.RefundPaymentAsync(id, request.Amount, request.Reason, userId, request.IdempotencyKey?.Trim());
            if (!result.Success)
                return BadRequest(new AdminPaymentActionResponse
                {
                    Success = false,
                    Message = result.Message,
                    Errors = result.Errors,
                    DiagnosticCode = result.DiagnosticCode
                });

            return Ok(new AdminPaymentActionResponse
            {
                Success = true,
                Message = result.Message,
                PaymentId = result.Payment?.Id,
                DiagnosticCode = result.DiagnosticCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin refund payment failed for PaymentId={PaymentId}", id);
            return StatusCode(500, new { message = "Refund operation failed", code = "ADMIN_PAYMENTS_REFUND_ERROR" });
        }
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
                .Where(x => _context.CashRegisters.Any(cr => cr.Id == x.CashRegisterId && cr.TenantId == tenantId))
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

    private static string ResolvePaymentStatus(PaymentDetails p)
    {
        if (p.IsStorno || !p.IsActive)
            return "Cancelled";
        if (p.IsRefund)
            return "Refunded";
        if (!string.IsNullOrWhiteSpace(p.FinanzOnlineStatus) &&
            string.Equals(p.FinanzOnlineStatus, "Failed", StringComparison.OrdinalIgnoreCase))
            return "Failed";
        if (!string.IsNullOrWhiteSpace(p.FinanzOnlineStatus) &&
            (string.Equals(p.FinanzOnlineStatus, "Pending", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(p.FinanzOnlineStatus, "NeedsReconciliation", StringComparison.OrdinalIgnoreCase)))
            return "Pending";
        return "Success";
    }

    private static string ParsePaymentMethodName(string rawValue)
    {
        if (int.TryParse(rawValue, out var methodInt) && Enum.IsDefined(typeof(PaymentMethod), methodInt))
            return ((PaymentMethod)methodInt).ToString();
        return "Unknown";
    }
}

public class AdminPaymentsListResponse
{
    public int Total { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public IReadOnlyList<AdminPaymentListItemDto> Items { get; set; } = Array.Empty<AdminPaymentListItemDto>();
}

public class AdminPaymentListItemDto
{
    public Guid Id { get; set; }
    public string? TransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Method { get; set; } = "Unknown";
    public string Status { get; set; } = "Success";
    public string? CustomerName { get; set; }
    public Guid CashRegisterId { get; set; }
    public string? ReceiptNumber { get; set; }
    public Guid? ReceiptId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public bool IsOfflineOrigin { get; set; }
    public Guid? OfflineTransactionId { get; set; }
    public Guid? OfflineReplayBatchCorrelationId { get; set; }
    public string? FinanzOnlineStatus { get; set; }
    public string? FinanzOnlineError { get; set; }
    public string? FinanzOnlineReferenceId { get; set; }
    public DateTime? FinanzOnlineLastAttemptAtUtc { get; set; }
    public int FinanzOnlineRetryCount { get; set; }
    public bool InvoicePersisted { get; set; }
    public decimal VoucherRedeemedAmount { get; set; }
    public bool HasVoucherRedemption { get; set; }
    public bool IsStorno { get; set; }
    public bool IsRefund { get; set; }
    public StornoReason? StornoReason { get; set; }
    public Guid? OriginalPaymentId { get; set; }
    public string? OriginalReceiptNumber { get; set; }
    public string? CashierDisplayName { get; set; }
    /// <summary>Completed when FinanzOnline (or derived pipeline) did not report failure; Failed otherwise.</summary>
    public string ReversalCompletionStatus { get; set; } = "Completed";
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
}

public class AdminPaymentActionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? PaymentId { get; set; }
    public string? DiagnosticCode { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
}
