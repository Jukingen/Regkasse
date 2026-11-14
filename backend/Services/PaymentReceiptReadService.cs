using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Logging;
using AuditLogStatus = KasseAPI_Final.Models.AuditLogStatus;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Reads a persisted receipt and records reprint confirmation.
    /// Does not allocate a BelegNr and does not create a TSE signature.
    /// </summary>
    public interface IPaymentReceiptReadService
    {
        Task<ReceiptDTO?> GetReceiptDataAsync(Guid paymentId, string? userId = null);

        Task<ReceiptReprintOperationResult> ConfirmReceiptReprintAsync(
            Guid paymentId,
            ReceiptReprintRequest? request,
            string userId,
            CancellationToken cancellationToken = default);
    }

    public sealed class PaymentReceiptReadService : IPaymentReceiptReadService
    {
        private readonly IReceiptService _receiptService;
        private readonly IGenericRepository<PaymentDetails> _paymentRepository;
        private readonly IAuditLogService _auditLogService;
        private readonly IUserService _userService;
        private readonly ILogger _logger;

        public PaymentReceiptReadService(
            IReceiptService receiptService,
            IGenericRepository<PaymentDetails> paymentRepository,
            IAuditLogService auditLogService,
            IUserService userService,
            ILogger logger)
        {
            _receiptService = receiptService;
            _paymentRepository = paymentRepository;
            _auditLogService = auditLogService;
            _userService = userService;
            _logger = logger;
        }

        /// <summary>
        /// Returns persisted receipt for payment. Receipt is created at payment time and includes totals, tax breakdown, signature, QR payload.
        /// When userId is provided, audit is written for ReceiptGenerated or ReceiptReprinted (append-only).
        /// </summary>
        public async Task<ReceiptDTO?> GetReceiptDataAsync(Guid paymentId, string? userId = null)
        {
            try
            {
                var receiptDto = await _receiptService.GetReceiptByPaymentIdAsync(paymentId);
                if (receiptDto == null)
                {
                    _logger.LogWarning("Receipt not found for payment {PaymentId} (receipt must be created at payment time)", paymentId);
                    return null;
                }

                // Audit: ReceiptGenerated (first) or ReceiptReprinted. Append-only; best-effort when userId provided.
                if (!string.IsNullOrEmpty(userId))
                {
                    var payment = await _paymentRepository.GetByIdAsync(paymentId);
                    var action = payment?.IsPrinted == true ? "ReceiptReprinted" : "ReceiptGenerated";
                    await LogPaymentAuditAsync(action, "Payment", paymentId, userId,
                        amount: receiptDto.GrandTotal,
                        description: $"{action} for payment {paymentId}",
                        responseData: new { PaymentId = paymentId, receiptDto.ReceiptNumber, IsPrinted = payment?.IsPrinted });
                }

                return receiptDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting receipt data for payment {PaymentId}", paymentId);
                return null;
            }
        }

        /// <summary>
        /// Backoffice: bestätigter Nachdruck — eine strukturierte Audit-Zeile (<c>ReceiptReprintConfirmed</c> / <c>ReceiptReprintRejected</c>), kein neuer Beleg, keine TSE-Neuerzeugung.
        /// </summary>
        public async Task<ReceiptReprintOperationResult> ConfirmReceiptReprintAsync(Guid paymentId, ReceiptReprintRequest? request, string userId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            const int maxNote = 500;
            static string? TrimCap(string? s, int max)
            {
                if (string.IsNullOrWhiteSpace(s))
                    return null;
                s = s.Trim();
                return s.Length <= max ? s : s.Substring(0, max);
            }

            var routing = new PrintRoutingContext
            {
                DeviceId = TrimCap(request?.DeviceId, 64),
                PrinterProfileId = TrimCap(request?.PrinterProfileId, 64),
                Resolved = false,
                IsSimulated = true
            };

            var requestSnapshot = new
            {
                ReprintReasonCode = request?.ReprintReasonCode,
                ReasonDetail = TrimCap(request?.ReasonDetail, maxNote),
                DeviceId = routing.DeviceId,
                PrinterProfileId = routing.PrinterProfileId,
                Note = TrimCap(request?.Note, maxNote),
                IdempotencyKey = TrimCap(request?.IdempotencyKey, 128)
            };

            if (request == null || string.IsNullOrWhiteSpace(request.ReprintReasonCode))
            {
                var id = await LogReceiptReprintAuditAsync("ReceiptReprintRejected", paymentId, userId, requestSnapshot,
                    new { ErrorCode = "VALIDATION_MISSING_REASON" }, AuditLogStatus.Failed, "Missing reprintReasonCode").ConfigureAwait(false);
                return new ReceiptReprintOperationResult
                {
                    Success = false,
                    ErrorCode = "VALIDATION_MISSING_REASON",
                    ErrorMessage = "reprintReasonCode is required.",
                    AuditLogId = id,
                    Routing = routing
                };
            }

            if (!ReceiptReprintReasonCodes.IsValid(request.ReprintReasonCode))
            {
                var id = await LogReceiptReprintAuditAsync("ReceiptReprintRejected", paymentId, userId, requestSnapshot,
                    new { ErrorCode = "VALIDATION_INVALID_REASON", ReasonCode = request.ReprintReasonCode }, AuditLogStatus.Failed, "Invalid reprintReasonCode").ConfigureAwait(false);
                return new ReceiptReprintOperationResult
                {
                    Success = false,
                    ErrorCode = "VALIDATION_INVALID_REASON",
                    ErrorMessage = "Invalid reprintReasonCode.",
                    AuditLogId = id,
                    Routing = routing
                };
            }

            var receiptDto = await _receiptService.GetReceiptByPaymentIdAsync(paymentId).ConfigureAwait(false);
            if (receiptDto == null)
            {
                var id = await LogReceiptReprintAuditAsync("ReceiptReprintRejected", paymentId, userId, requestSnapshot,
                    new { ErrorCode = "NOT_FOUND" }, AuditLogStatus.Failed, "No persisted receipt for payment").ConfigureAwait(false);
                return new ReceiptReprintOperationResult
                {
                    Success = false,
                    NotFound = true,
                    ErrorCode = "NOT_FOUND",
                    ErrorMessage = "No persisted receipt for this payment.",
                    AuditLogId = id,
                    Routing = routing
                };
            }

            var successPayload = new
            {
                ReprintReasonCode = request.ReprintReasonCode.Trim(),
                ReasonDetail = TrimCap(request.ReasonDetail, maxNote),
                DeviceId = routing.DeviceId,
                PrinterProfileId = routing.PrinterProfileId,
                Note = TrimCap(request.Note, maxNote),
                IdempotencyKey = TrimCap(request?.IdempotencyKey, 128),
                ReceiptNumber = receiptDto.ReceiptNumber,
                ReceiptId = receiptDto.ReceiptId,
                RoutingSimulated = true,
                ReportableEventType = "ReceiptReprintConfirmed"
            };

            var auditId = await LogReceiptReprintAuditAsync("ReceiptReprintConfirmed", paymentId, userId, requestSnapshot,
                successPayload, AuditLogStatus.Success, null, receiptDto.GrandTotal).ConfigureAwait(false);

            return new ReceiptReprintOperationResult
            {
                Success = true,
                Receipt = receiptDto,
                AuditLogId = auditId,
                Routing = routing
            };
        }

        /// <summary>Nachdruck-Audit (append-only); Fehler beim Schreiben werden geschluckt wie bei anderen Payment-Audits.</summary>
        private async Task<Guid?> LogReceiptReprintAuditAsync(
            string action,
            Guid paymentId,
            string userId,
            object requestSnapshot,
            object? responseSnapshot,
            AuditLogStatus status,
            string? errorDetails,
            decimal? amount = null)
        {
            try
            {
                var userRole = await GetUserRoleAsync(userId).ConfigureAwait(false);
                var audit = await _auditLogService.LogPaymentOperationAsync(
                    action,
                    "Payment",
                    paymentId,
                    userId,
                    userRole,
                    amount: amount,
                    requestData: requestSnapshot,
                    responseData: responseSnapshot,
                    status: status,
                    errorDetails: errorDetails,
                    description: $"Receipt reprint {action} for payment {paymentId}").ConfigureAwait(false);
                return audit.Id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit log write failed for receipt reprint {Action} PaymentId={PaymentId}", action, paymentId);
                return null;
            }
        }

        /// <summary>Writes payment lifecycle audit log after successful commit. Append-only; never updates. Swallows exceptions to avoid affecting caller.</summary>
        private async Task LogPaymentAuditAsync(string action, string entityType, Guid entityId, string userId,
            decimal? amount = null, string? paymentMethod = null, string? tseSignature = null, string? description = null,
            object? responseData = null, string? correlationId = null)
        {
            try
            {
                var userRole = await GetUserRoleAsync(userId);
                await _auditLogService.LogPaymentOperationAsync(action, entityType, entityId, userId, userRole,
                    amount: amount, paymentMethod: paymentMethod, tseSignature: tseSignature,
                    correlationId: correlationId,
                    description: description, responseData: responseData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit log write failed for action {Action} EntityType={EntityType} EntityId={EntityId}; payment operation already committed.", action, entityType, entityId);
            }
        }

        private async Task<string> GetUserRoleAsync(string userId)
        {
            var user = await _userService.GetUserByIdAsync(userId);
            return user?.Role ?? "Unknown";
        }
    }
}
