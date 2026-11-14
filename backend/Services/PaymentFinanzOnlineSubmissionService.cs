using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AuditLogStatus = KasseAPI_Final.Models.AuditLogStatus;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// FinanzOnline submit and retry. Callers invoke this after the payment commit.
    /// Failures are best-effort and do not roll back the fiscal transaction.
    /// </summary>
    public interface IPaymentFinanzOnlineSubmissionService
    {
        Task<bool> SendToFinanzOnlineAsync(PaymentDetails payment);

        Task<FinanzOnlineSubmitResponse> RetryFinanzOnlineSubmitAsync(Guid paymentId);

        Task UpdatePaymentFinanzOnlineStateAsync(
            Guid paymentId,
            FinanzOnlineSubmitResponse result,
            bool isRetry,
            string? userIdForAudit = null,
            string? correlationIdForAudit = null);
    }

    public sealed class PaymentFinanzOnlineSubmissionService : IPaymentFinanzOnlineSubmissionService
    {
        private readonly AppDbContext _context;
        private readonly ICompanyProfileProvider _companyProfileProvider;
        private readonly IFinanzOnlineService _finanzOnlineService;
        private readonly IFinanzOnlineMetrics? _finanzOnlineMetrics;
        private readonly IAuditLogService _auditLogService;
        private readonly IUserService _userService;
        private readonly ISettingsTenantResolver _settingsTenantResolver;
        private readonly ILogger _logger;

        public PaymentFinanzOnlineSubmissionService(
            AppDbContext context,
            ICompanyProfileProvider companyProfileProvider,
            IFinanzOnlineService finanzOnlineService,
            IAuditLogService auditLogService,
            IUserService userService,
            ISettingsTenantResolver settingsTenantResolver,
            ILogger logger,
            IFinanzOnlineMetrics? finanzOnlineMetrics = null)
        {
            _context = context;
            _companyProfileProvider = companyProfileProvider;
            _finanzOnlineService = finanzOnlineService;
            _auditLogService = auditLogService;
            _userService = userService;
            _settingsTenantResolver = settingsTenantResolver;
            _logger = logger;
            _finanzOnlineMetrics = finanzOnlineMetrics;
        }

        /// <summary>
        /// FinanzOnline entegrasyonu
        /// </summary>
        public async Task<bool> SendToFinanzOnlineAsync(PaymentDetails payment)
        {
            try
            {
                var companyProfile = await _companyProfileProvider.GetCompanyProfileAsync().ConfigureAwait(false);

                // TSE imzası kontrolü - FinanzOnline için zorunlu
                if (string.IsNullOrEmpty(payment.TseSignature))
                {
                    _logger.LogWarning("Payment {PaymentId} has no TSE signature. Cannot send to FinanzOnline", payment.Id);
                    return false;
                }

                // PaymentDetails'den Invoice oluştur
                var invoice = new Invoice
                {
                    InvoiceNumber = payment.Id.ToString(),
                    InvoiceDate = payment.CreatedAt,
                    DueDate = payment.CreatedAt.AddDays(30),
                    Status = InvoiceStatus.Paid,
                    Subtotal = payment.TotalAmount - payment.TaxAmount,
                    TaxAmount = payment.TaxAmount,
                    TotalAmount = payment.TotalAmount,
                    PaidAmount = payment.TotalAmount,
                    RemainingAmount = 0,
                    CustomerName = payment.CustomerName,
                    CompanyName = payment.CompanyName ?? companyProfile.CompanyName,
                    CompanyTaxNumber = payment.Steuernummer,
                    CompanyAddress = payment.CompanyAddress
                        ?? CompanyProfileMapper.FormatCompanyAddress(
                            companyProfile.Street, companyProfile.ZipCode, companyProfile.City),
                    TseSignature = payment.TseSignature,
                    KassenId = (await _context.CashRegisters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == payment.CashRegisterId))?.RegisterNumber
                        ?? throw new InvalidOperationException("Cash register not found for FinanzOnline submit."),
                    TseTimestamp = payment.CreatedAt,
                    CashRegisterId = payment.CashRegisterId,
                    PaymentMethod = payment.PaymentMethod,
                    PaymentReference = payment.TransactionId,
                    PaymentDate = payment.CreatedAt
                };
                FiscalDocumentCountryStamp.CopyFromPayment(invoice, payment);

                var result = await _finanzOnlineService.SubmitInvoiceAsync(invoice);

                if (result.Success)
                {
                    _logger.LogInformation("Payment {PaymentId} successfully sent to FinanzOnline", payment.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to send payment {PaymentId} to FinanzOnline: {Error}",
                        payment.Id, result.ErrorMessage);
                }

                return result.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending payment to FinanzOnline: {PaymentId}", payment.Id);
                return false;
            }
        }

        /// <summary>Retry FinanzOnline submit for a payment (reconciliation). No-op if already Submitted.</summary>
        public async Task<FinanzOnlineSubmitResponse> RetryFinanzOnlineSubmitAsync(Guid paymentId)
        {
            var payment = await _context.PaymentDetails.AsNoTracking().FirstOrDefaultAsync(p => p.Id == paymentId).ConfigureAwait(false);
            if (payment == null)
            {
                return new FinanzOnlineSubmitResponse
                {
                    Success = false,
                    ErrorMessage = "Payment not found.",
                    Status = "Failed",
                    SubmittedAt = DateTime.UtcNow,
                    FailureKind = FinanzOnlineFailureKind.Permanent
                };
            }
            if (!await PaymentBelongsToEffectiveTenantAsync(payment).ConfigureAwait(false))
            {
                return new FinanzOnlineSubmitResponse
                {
                    Success = false,
                    ErrorMessage = "Payment not found.",
                    Status = "Failed",
                    SubmittedAt = DateTime.UtcNow,
                    FailureKind = FinanzOnlineFailureKind.Permanent
                };
            }
            if (payment.FinanzOnlineStatus == "Submitted")
            {
                _finanzOnlineMetrics?.IncrementSubmitTotal();
                return new FinanzOnlineSubmitResponse
                {
                    Success = true,
                    ReferenceId = payment.FinanzOnlineReferenceId,
                    Status = "Submitted",
                    SubmittedAt = payment.FinanzOnlineLastAttemptAtUtc ?? DateTime.UtcNow,
                    FailureKind = FinanzOnlineFailureKind.None
                };
            }
            _finanzOnlineMetrics?.IncrementSubmitTotal();
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.SourcePaymentId == paymentId).ConfigureAwait(false);
            if (invoice == null)
            {
                return new FinanzOnlineSubmitResponse
                {
                    Success = false,
                    ErrorMessage = "Invoice not found for payment.",
                    Status = "Failed",
                    SubmittedAt = DateTime.UtcNow,
                    FailureKind = FinanzOnlineFailureKind.Permanent
                };
            }
            var foCorrelationId = payment.OfflineReplayBatchCorrelationId?.ToString("N") ?? paymentId.ToString("N");
            try
            {
                var result = await _finanzOnlineService.SubmitInvoiceAsync(invoice).ConfigureAwait(false);
                if (!result.Success)
                    _finanzOnlineMetrics?.IncrementSubmitFailed(result.FailureKind);
                await UpdatePaymentFinanzOnlineStateAsync(paymentId, result, isRetry: true,
                    userIdForAudit: "system",
                    correlationIdForAudit: foCorrelationId).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                var classified = ClassifyFinanzOnlineFailure(ex);
                _finanzOnlineMetrics?.IncrementSubmitFailed(classified);
                var response = new FinanzOnlineSubmitResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    SubmittedAt = DateTime.UtcNow,
                    Status = "Failed",
                    FailureKind = classified
                };
                await UpdatePaymentFinanzOnlineStateAsync(paymentId, response, isRetry: true,
                    userIdForAudit: "system",
                    correlationIdForAudit: foCorrelationId).ConfigureAwait(false);
                return response;
            }
        }

        /// <summary>Persist FinanzOnline reconciliation state on PaymentDetails after submit (post-commit). Best-effort; does not throw. Logs FO attempt to audit for incident investigation (correlation id + retry history).</summary>
        public async Task UpdatePaymentFinanzOnlineStateAsync(Guid paymentId, FinanzOnlineSubmitResponse result, bool isRetry,
            string? userIdForAudit = null,
            string? correlationIdForAudit = null)
        {
            try
            {
                var payment = await _context.PaymentDetails.FindAsync(paymentId).ConfigureAwait(false);
                if (payment == null)
                    return;

                payment.FinanzOnlineLastAttemptAtUtc = result.SubmittedAt;
                payment.FinanzOnlineError = result.Success ? null : TruncateForDb(result.ErrorMessage, 500);
                payment.FinanzOnlineReferenceId = result.ReferenceId;
                if (isRetry)
                    payment.FinanzOnlineRetryCount++;

                payment.FinanzOnlineStatus = result.Success
                    ? "Submitted"
                    : result.FailureKind == FinanzOnlineFailureKind.Transient
                        ? "Pending"
                        : "Failed";

                await _context.SaveChangesAsync().ConfigureAwait(false);

                var correlationId = correlationIdForAudit ?? payment.OfflineReplayBatchCorrelationId?.ToString("N") ?? paymentId.ToString("N");
                var userId = userIdForAudit ?? "system";
                await LogFinanzOnlineAttemptAsync(
                    paymentId,
                    result,
                    isRetry,
                    payment.FinanzOnlineRetryCount,
                    correlationId,
                    userId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update PaymentDetails FinanzOnline state for PaymentId={PaymentId}; reconciliation view may be stale.", paymentId);
            }
        }

        /// <summary>Audit one FinanzOnline submit/retry attempt for incident investigation (support can query by CorrelationId).</summary>
        private async Task LogFinanzOnlineAttemptAsync(Guid paymentId, FinanzOnlineSubmitResponse result, bool isRetry, int attemptNumber, string correlationId, string userId)
        {
            try
            {
                var action = isRetry ? "FinanzOnlineRetry" : "FinanzOnlineSubmit";
                var userRole = string.Equals(userId, "system", StringComparison.OrdinalIgnoreCase) ? "System" : await GetUserRoleAsync(userId).ConfigureAwait(false);
                await _auditLogService.LogPaymentOperationAsync(
                    action,
                    "Payment",
                    paymentId,
                    userId,
                    userRole,
                    description: $"{action} attempt {attemptNumber}: {(result.Success ? "Submitted" : "Failed")}",
                    responseData: new
                    {
                        Attempt = attemptNumber,
                        Success = result.Success,
                        ReferenceId = result.ReferenceId,
                        FailureKind = result.FailureKind.ToString(),
                        ErrorMessage = result.Success ? null : result.ErrorMessage,
                        CorrelationId = correlationId
                    },
                    correlationId: correlationId,
                    status: result.Success ? AuditLogStatus.Success : AuditLogStatus.Failed,
                    errorDetails: result.Success ? null : result.ErrorMessage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FinanzOnline attempt audit log failed for PaymentId={PaymentId}.", paymentId);
            }
        }

        public static FinanzOnlineFailureKind ClassifyFinanzOnlineFailure(Exception ex)
        {
            if (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
                return FinanzOnlineFailureKind.Transient;
            var msg = (ex.Message ?? "").ToLowerInvariant();
            if (msg.Contains("duplicate") || msg.Contains("already submitted") || msg.Contains("validation") || msg.Contains("forbidden"))
                return FinanzOnlineFailureKind.Permanent;
            return FinanzOnlineFailureKind.Unknown;
        }

        private static string? TruncateForDb(string? value, int maxLen)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            return value.Length <= maxLen ? value : value.Substring(0, maxLen - 3) + "...";
        }

        private async Task<string> GetUserRoleAsync(string userId)
        {
            var user = await _userService.GetUserByIdAsync(userId);
            return user?.Role ?? "Unknown";
        }

        private async Task<bool> PaymentBelongsToEffectiveTenantAsync(PaymentDetails payment, CancellationToken cancellationToken = default)
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            return await _context.CashRegisters.AsNoTracking().ForResolvedTenantScope()
                .AnyAsync(cr => cr.Id == payment.CashRegisterId && cr.TenantId == tenantId, cancellationToken);
        }
    }
}
