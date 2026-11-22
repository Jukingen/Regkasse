using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services;

/// <summary>
/// Payment audit, FinanzOnline submission, and card attachment.
/// Runs only after the fiscal transaction has committed. Failures are logged and do not roll back the sale.
/// </summary>
public interface IPaymentPostCommitService
{
    Task<PaymentResult> DispatchPostCommitAsync(
        CreatePaymentRequest request,
        string userId,
        Customer customer,
        Guid tenantIdForFlags,
        PaymentDetails createdPayment,
        Invoice createdInvoice,
        IReadOnlyList<IssuedTicketDto> committedIssuedTickets,
        int paymentStockLinesSkipped,
        bool effectiveTseRequired,
        Guid? offlineReplayBatchCorrelationId);
}

/// <summary>
/// Writes the payment-created audit and, when a canonical receipt row exists, the receipt-persisted audit.
/// </summary>
public delegate Task PaymentAuditWriter(
    string action,
    string entityType,
    Guid entityId,
    string userId,
    decimal? amount = null,
    string? paymentMethod = null,
    string? tseSignature = null,
    string? description = null,
    object? responseData = null,
    string? correlationId = null);

/// <summary>Builds the RKSV QR payload and demo/provider flags for a committed payment.</summary>
public delegate Task<(string QrPayload, bool IsDemoFiscal, string TseProvider)> PaymentQrPayloadBuilder(
    PaymentDetails payment,
    bool tseRequired);

public sealed class PaymentPostCommitService : IPaymentPostCommitService
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;
    private readonly IAuditLogService _auditLogService;
    private readonly IFinanzOnlineService _finanzOnlineService;
    private readonly IPaymentFinanzOnlineSubmissionService _finanzOnlineSubmission;
    private readonly InventoryOptions _inventoryOptions;
    private readonly PaymentAuditWriter _logPaymentAudit;
    private readonly PaymentQrPayloadBuilder _buildQrPayload;
    private readonly IFinanzOnlineMetrics? _finanzOnlineMetrics;
    private readonly ICardPaymentService? _cardPaymentService;

    public PaymentPostCommitService(
        AppDbContext context,
        ILogger<PaymentService> logger,
        IAuditLogService auditLogService,
        IFinanzOnlineService finanzOnlineService,
        IPaymentFinanzOnlineSubmissionService finanzOnlineSubmission,
        InventoryOptions inventoryOptions,
        PaymentAuditWriter logPaymentAudit,
        PaymentQrPayloadBuilder buildQrPayload,
        IFinanzOnlineMetrics? finanzOnlineMetrics = null,
        ICardPaymentService? cardPaymentService = null)
    {
        _context = context;
        _logger = logger;
        _auditLogService = auditLogService;
        _finanzOnlineService = finanzOnlineService;
        _finanzOnlineSubmission = finanzOnlineSubmission;
        _inventoryOptions = inventoryOptions;
        _logPaymentAudit = logPaymentAudit;
        _buildQrPayload = buildQrPayload;
        _finanzOnlineMetrics = finanzOnlineMetrics;
        _cardPaymentService = cardPaymentService;
    }

    /// <summary>
    /// Audits, FinanzOnline dispatch, card-intent link, and the success result after the fiscal commit.
    /// </summary>
    public async Task<PaymentResult> DispatchPostCommitAsync(
        CreatePaymentRequest request,
        string userId,
        Customer customer,
        Guid tenantIdForFlags,
        PaymentDetails createdPayment,
        Invoice createdInvoice,
        IReadOnlyList<IssuedTicketDto> committedIssuedTickets,
        int paymentStockLinesSkipped,
        bool effectiveTseRequired,
        Guid? offlineReplayBatchCorrelationId)
    {
        if (!_inventoryOptions.EnforceStockOnSales && paymentStockLinesSkipped > 0)
        {
            _logger.LogInformation(
                "Payment {PaymentId} committed without per-line stock mutations; skippedProductLines={SkippedLines} (Inventory:EnforceStockOnSales=false).",
                createdPayment.Id,
                paymentStockLinesSkipped);
        }

        _logger.LogInformation("Payment created successfully: {PaymentId} for customer {CustomerId} (Invoice {InvoiceId}, Receipt in same transaction)",
            createdPayment.Id, customer.Id, createdInvoice.Id);

        await DispatchPostCommitComplianceAsync(createdPayment, createdInvoice, userId, offlineReplayBatchCorrelationId, effectiveTseRequired);

        if (!string.IsNullOrEmpty(createdPayment.PrescriptionReference))
        {
            try
            {
                await _auditLogService.LogSystemOperationAsync(
                    action: "PAYMENT_WITH_PRESCRIPTION",
                    entityType: "Payment",
                    userId: userId,
                    userRole: "Cashier",
                    description: "Payment stored a non-fiscal prescription reference",
                    actionType: AuditEventType.PaymentWithPrescription,
                    entityId: createdPayment.Id,
                    tenantId: tenantIdForFlags,
                    newValues: new { hasPrescription = true });
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning(
                    auditEx,
                    "PaymentWithPrescription audit write failed for {PaymentId}",
                    createdPayment.Id);
            }
        }

        if (committedIssuedTickets.Count > 0)
        {
            try
            {
                await _auditLogService.LogSystemOperationAsync(
                    action: "TICKET_ISSUED",
                    entityType: "Payment",
                    userId: userId,
                    userRole: "Cashier",
                    description: "Ticket-sales sale issued redeemable tickets",
                    actionType: AuditEventType.TicketIssued,
                    entityId: createdPayment.Id,
                    tenantId: tenantIdForFlags,
                    newValues: new { ticketCount = committedIssuedTickets.Count });
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning(
                    auditEx,
                    "TicketIssued audit write failed for {PaymentId}",
                    createdPayment.Id);
            }
        }

        if (_cardPaymentService != null)
        {
            var linkedIntentId = request.Payment.CardPaymentIntentId
                ?? request.Payment.OnlinePaymentId
                ?? request.Payment.GatewayIntentId;
            if (linkedIntentId is Guid cardIntentId && cardIntentId != Guid.Empty)
            {
                await _cardPaymentService.LinkToPaymentAsync(cardIntentId, createdPayment.Id).ConfigureAwait(false);
            }
        }

        var (qrPayload, isDemoFiscal, tseProvider) = await _buildQrPayload(createdPayment, effectiveTseRequired);
        return new PaymentResult
        {
            Success = true,
            Message = "Payment created successfully",
            Payment = createdPayment,
            PaymentId = createdPayment.Id,
            TseSignature = createdPayment.TseSignature,
            QrPayload = qrPayload,
            IsDemoFiscal = isDemoFiscal,
            TseProvider = tseProvider,
            InvoicePersisted = true,
            TimeSyncWarning = createdPayment.TimeSyncWarning,
            IssuedTickets = committedIssuedTickets
        };
    }

    public async Task DispatchPostCommitComplianceAsync(
        PaymentDetails createdPayment,
        Invoice createdInvoice,
        string userId,
        Guid? offlineReplayBatchCorrelationId,
        bool effectiveTseRequired)
    {
        try
        {
            // Audit: append-only, after successful commit. Best-effort; failure does not roll back payment.
            var paymentAuditCorrelation = offlineReplayBatchCorrelationId?.ToString("N");
            await _logPaymentAudit("PaymentCreated", "Payment", createdPayment.Id, userId,
                amount: createdPayment.TotalAmount,
                paymentMethod: createdPayment.PaymentMethodRaw,
                tseSignature: createdPayment.TseSignature,
                correlationId: paymentAuditCorrelation,
                responseData: new
                {
                    createdPayment.Id,
                    createdPayment.ReceiptNumber,
                    createdPayment.TotalAmount,
                    createdPayment.CashRegisterId,
                    CreatedAt = createdPayment.CreatedAt,
                    offlineReplayBatchCorrelationId = offlineReplayBatchCorrelationId
                });

            var persistedReceipt = await _context.Receipts.AsNoTracking()
                .FirstOrDefaultAsync(r => r.PaymentId == createdPayment.Id);
            if (persistedReceipt != null)
            {
                await _logPaymentAudit("ReceiptPersisted", "Receipt", persistedReceipt.ReceiptId, userId,
                    amount: createdPayment.TotalAmount,
                    tseSignature: createdPayment.TseSignature,
                    correlationId: paymentAuditCorrelation,
                    description: "Canonical fiscal receipt persisted with payment",
                    responseData: new
                    {
                        persistedReceipt.ReceiptId,
                        persistedReceipt.ReceiptNumber,
                        PaymentId = createdPayment.Id,
                        createdPayment.CashRegisterId,
                        offlineReplayBatchCorrelationId = offlineReplayBatchCorrelationId
                    });
            }

            // FinanzOnline: best-effort after commit; failure does not roll back DB.
            if (effectiveTseRequired)
            {
                try
                {
                    _finanzOnlineMetrics?.IncrementSubmitTotal();
                    var foResult = await _finanzOnlineService.SubmitInvoiceAsync(createdInvoice).ConfigureAwait(false);
                    await _finanzOnlineSubmission.UpdatePaymentFinanzOnlineStateAsync(createdPayment.Id, foResult, isRetry: false,
                        userIdForAudit: userId,
                        correlationIdForAudit: offlineReplayBatchCorrelationId?.ToString("N")).ConfigureAwait(false);
                    if (foResult.Success)
                        _logger.LogInformation("Invoice sent to FinanzOnline: {InvoiceId}, ReferenceId={RefId}", createdInvoice.Id, foResult.ReferenceId);
                    else
                    {
                        _finanzOnlineMetrics?.IncrementSubmitFailed(foResult.FailureKind);
                        _logger.LogWarning("FinanzOnline submit failed for Invoice {InvoiceId}: {Error}, FailureKind={Kind}", createdInvoice.Id, foResult.ErrorMessage, foResult.FailureKind);
                    }
                }
                catch (Exception ex)
                {
                    var kind = PaymentFinanzOnlineSubmissionService.ClassifyFinanzOnlineFailure(ex);
                    _finanzOnlineMetrics?.IncrementSubmitFailed(kind);
                    _logger.LogWarning(ex, "Failed to send invoice to FinanzOnline: {InvoiceId}", createdInvoice.Id);
                    await _finanzOnlineSubmission.UpdatePaymentFinanzOnlineStateAsync(createdPayment.Id, new FinanzOnlineSubmitResponse
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        SubmittedAt = DateTime.UtcNow,
                        Status = "Failed",
                        FailureKind = kind
                    }, isRetry: false,
                        userIdForAudit: userId,
                        correlationIdForAudit: offlineReplayBatchCorrelationId?.ToString("N")).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post-commit compliance orchestration failed for payment {PaymentId}. The payment was successfully committed, but secondary actions failed.", createdPayment.Id);
        }
    }
}
