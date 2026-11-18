using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Services.Preorder;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KasseAPI_Final.Services
{
    public interface IPaymentStornoService
    {
        Task<PaymentResult> CancelPaymentAsync(
            Guid paymentId,
            string reason,
            string userId,
            string? idempotencyKey = null,
            CancellationReasonCode reasonCode = CancellationReasonCode.Other,
            string? approvalToken = null);

        Task<PaymentResult> CreateStornoReversalAsync(
            PaymentDetails payment,
            string reason,
            string userId,
            string? cancelIdempotencyKey,
            StornoReason? stornoReasonEnum = null);

        Task<PaymentDetails?> FindOriginalSalePaymentByReceiptNumberAsync(Guid cashRegisterId, string receiptNumber);

        string FormatStornoReasonForAudit(StornoReason reason);
    }

    internal delegate Task<bool> PaymentTenantMatch(PaymentDetails payment, CancellationToken cancellationToken = default);

    internal delegate Task<string> AllocateCountryBelegNr(
        CountryStrategyBinding countryBinding,
        IDbContextTransaction transaction,
        Guid cashRegisterId,
        string registerNumber);

    internal delegate Task RunPaymentHook(
        Guid tenantId,
        string hookName,
        Guid paymentId,
        decimal? totalAmount = null,
        string? receiptNumber = null);

    internal delegate Task LogPaymentAudit(
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

    internal delegate Task LogFiskalyCancellationAudit(
        PaymentDetails storno,
        string userId,
        string signingProvider,
        string reason);

    /// <summary>
    /// Fiscal storno (full reversal). Owns the storno transaction; card refund runs after commit.
    /// </summary>
    public sealed class PaymentStornoService : IPaymentStornoService
    {
        private readonly AppDbContext _context;
        private readonly IGenericRepository<PaymentDetails> _paymentRepository;
        private readonly IUserService _userService;
        private readonly IReceiptService _receiptService;
        private readonly ICompanyProfileProvider _companyProfileProvider;
        private readonly ISettingsTenantResolver _settingsTenantResolver;
        private readonly ICountryStrategyContext _countryStrategyContext;
        private readonly InventoryOptions _inventoryOptions;
        private readonly ITseService _tseService;
        private readonly IPaymentVoucherLedgerService _voucherLedger;
        private readonly IPaymentReversalApprovalService _reversalApproval;
        private readonly ICardPaymentService? _cardPaymentService;
        private readonly IPreorderService? _preorderService;
        private readonly ILogger _logger;
        private readonly PaymentTenantMatch _belongsToTenant;
        private readonly AllocateCountryBelegNr _allocateBelegNr;
        private readonly RunPaymentHook _runPaymentHook;
        private readonly LogPaymentAudit _logPaymentAudit;
        private readonly LogFiskalyCancellationAudit _logFiskalyCancellation;

        internal PaymentStornoService(
            AppDbContext context,
            IGenericRepository<PaymentDetails> paymentRepository,
            IUserService userService,
            IReceiptService receiptService,
            ICompanyProfileProvider companyProfileProvider,
            ISettingsTenantResolver settingsTenantResolver,
            ICountryStrategyContext countryStrategyContext,
            InventoryOptions inventoryOptions,
            ITseService tseService,
            IPaymentVoucherLedgerService voucherLedger,
            IPaymentReversalApprovalService reversalApproval,
            ILogger logger,
            ICardPaymentService? cardPaymentService,
            IPreorderService? preorderService,
            PaymentTenantMatch belongsToTenant,
            AllocateCountryBelegNr allocateBelegNr,
            RunPaymentHook runPaymentHook,
            LogPaymentAudit logPaymentAudit,
            LogFiskalyCancellationAudit logFiskalyCancellation)
        {
            _context = context;
            _paymentRepository = paymentRepository;
            _userService = userService;
            _receiptService = receiptService;
            _companyProfileProvider = companyProfileProvider;
            _settingsTenantResolver = settingsTenantResolver;
            _countryStrategyContext = countryStrategyContext;
            _inventoryOptions = inventoryOptions;
            _tseService = tseService;
            _voucherLedger = voucherLedger;
            _reversalApproval = reversalApproval;
            _logger = logger;
            _cardPaymentService = cardPaymentService;
            _preorderService = preorderService;
            _belongsToTenant = belongsToTenant;
            _allocateBelegNr = allocateBelegNr;
            _runPaymentHook = runPaymentHook;
            _logPaymentAudit = logPaymentAudit;
            _logFiskalyCancellation = logFiskalyCancellation;
        }

        private Task<bool> PaymentBelongsToEffectiveTenantAsync(PaymentDetails payment, CancellationToken cancellationToken = default) =>
            _belongsToTenant(payment, cancellationToken);

        private Task<string> AllocateCountryBelegNrAsync(
            CountryStrategyBinding countryBinding,
            IDbContextTransaction transaction,
            Guid cashRegisterId,
            string registerNumber) =>
            _allocateBelegNr(countryBinding, transaction, cashRegisterId, registerNumber);

        private Task RunPaymentHookAsync(
            Guid tenantId,
            string hookName,
            Guid paymentId,
            decimal? totalAmount = null,
            string? receiptNumber = null) =>
            _runPaymentHook(tenantId, hookName, paymentId, totalAmount, receiptNumber);

        private Task LogPaymentAuditAsync(
            string action,
            string entityType,
            Guid entityId,
            string userId,
            decimal? amount = null,
            string? paymentMethod = null,
            string? tseSignature = null,
            string? description = null,
            object? responseData = null,
            string? correlationId = null) =>
            _logPaymentAudit(action, entityType, entityId, userId, amount, paymentMethod, tseSignature, description, responseData, correlationId);

        private Task LogFiskalyCancellationAuditAsync(
            PaymentDetails storno,
            string userId,
            string signingProvider,
            string reason) =>
            _logFiskalyCancellation(storno, userId, signingProvider, reason);

        private static PaymentResult? MapApprovalGateFailure(PaymentReversalApprovalGateOutcome gate) =>
            PaymentService.MapApprovalGateFailure(gate);

        private static PaymentResult ExtensionRejected(ExtensionFailedException ex) =>
            PaymentService.ExtensionRejected(ex);

        private static bool IsCancelIdempotencyKeyViolation(DbUpdateException ex) =>
            PaymentCommitFailureMapper.IsCancelIdempotencyKeyViolation(ex);

        private static bool IsStornoUniqueViolation(DbUpdateException ex) =>
            PaymentCommitFailureMapper.IsStornoUniqueViolation(ex);

        private async Task TryMarkPreorderCancelledAsync(Guid sourcePaymentId)
        {
            if (_preorderService is null || sourcePaymentId == Guid.Empty)
                return;

            try
            {
                await _preorderService.MarkCancelledForPaymentAsync(sourcePaymentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Preorder cancel mark failed for payment {PaymentId}", sourcePaymentId);
            }
        }

        /// <summary>
        /// Cancel payment via fiscal storno (reversal). Original payment is NEVER modified; a reversal record is created with TSE signature and credit note.
        /// Sprint 6: optional idempotencyKey — retries with same key return existing storno.
        /// </summary>
        public async Task<PaymentResult> CancelPaymentAsync(
            Guid paymentId,
            string reason,
            string userId,
            string? idempotencyKey = null,
            CancellationReasonCode reasonCode = CancellationReasonCode.Other,
            string? approvalToken = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Cancellation reason is required (minimum 5 characters)",
                        Errors = { "Reason must be at least 5 characters" },
                        DiagnosticCode = "CANCELLATION_REASON_REQUIRED",
                        IsDeterministicFailure = true
                    };
                }

                var user = await _userService.GetUserByIdAsync(userId);
                if (DemoUserHelper.IsDemoUser(user))
                {
                    var rejectionReason = DemoUserHelper.GetDemoRejectionReason(user) ?? "DEMO_UNKNOWN";
                    _logger.LogWarning(
                        "Payment cancel demo rejection: AuthenticatedUserId={AuthenticatedUserId} PaymentId={PaymentId} RejectionCode={RejectionCode}",
                        userId, paymentId, rejectionReason);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Demo users cannot cancel real payments",
                        Errors = { "Demo users are restricted to test operations only" },
                        DiagnosticCode = rejectionReason
                    };
                }

                var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
                if (key != null)
                {
                    var existingStorno = await _context.PaymentDetails.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.CancelIdempotencyKey == key && p.IsStorno);
                    if (existingStorno != null)
                    {
                        if (!await PaymentBelongsToEffectiveTenantAsync(existingStorno))
                        {
                            return new PaymentResult
                            {
                                Success = false,
                                Message = "Payment not found",
                                Errors = { "Payment not found" }
                            };
                        }
                        _logger.LogInformation("Idempotent cancel: returning existing storno {StornoId} for payment {PaymentId} key {Key}", existingStorno.Id, paymentId, key);
                        await TryMarkPreorderCancelledAsync(paymentId);
                        var replayRefundFailure = await RefundCardAfterFiscalStornoAsync(
                                existingStorno.OriginalPaymentId ?? paymentId,
                                Math.Abs(existingStorno.TotalAmount))
                            .ConfigureAwait(false);
                        if (replayRefundFailure != null)
                            return replayRefundFailure;
                        return new PaymentResult
                        {
                            Success = true,
                            Message = "Payment cancelled successfully",
                            Payment = existingStorno
                        };
                    }
                }

                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment not found",
                        Errors = { "Payment not found" }
                    };
                }

                if (!await PaymentBelongsToEffectiveTenantAsync(payment))
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment not found",
                        Errors = { "Payment not found" }
                    };
                }

                var hasRefundRows = await _context.PaymentDetails.AsNoTracking()
                    .AnyAsync(p => p.OriginalPaymentId == paymentId && p.IsRefund && p.IsActive);
                if (hasRefundRows)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Cannot cancel: partial refunds exist for this payment",
                        Errors = { "Full storno is not allowed after partial refunds on the same sale." },
                        DiagnosticCode = "STORNO_BLOCKED_BY_REFUNDS",
                        IsDeterministicFailure = true
                    };
                }

                // Already cancelled = a storno (reversal) already exists for this payment. Original payment is never modified.
                var alreadyHasStorno = await _context.PaymentDetails.AsNoTracking()
                    .AnyAsync(p => p.OriginalPaymentId == paymentId && p.IsStorno);
                if (alreadyHasStorno)
                {
                    var refundFailure = await RefundCardAfterFiscalStornoAsync(paymentId, payment.TotalAmount)
                        .ConfigureAwait(false);
                    if (refundFailure != null)
                        return refundFailure;

                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment is already cancelled",
                        Errors = { "A reversal (storno) already exists for this payment" },
                        DiagnosticCode = "STORNO_ALREADY_EXISTS",
                        IsDeterministicFailure = true
                    };
                }

                // Legacy: if payment was previously cancelled by old flow (IsActive=false), do not create duplicate storno
                if (!payment.IsActive)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment is already cancelled",
                        Errors = { "Payment has already been cancelled (legacy)" }
                    };
                }

                // RKSV: reversal row must carry a concrete StornoReason mapped from reason code.
                var formattedReason = PaymentReversalReasonMapper.FormatCancellationReason(reasonCode, reason);
                var stornoReason = PaymentReversalReasonMapper.ToStornoReason(reasonCode);

                var approvalGate = await _reversalApproval.EnforceApprovalAsync(
                    payment,
                    PaymentReversalOperation.Cancel,
                    null,
                    formattedReason,
                    (int)reasonCode,
                    userId,
                    approvalToken,
                    key,
                    CancellationToken.None);
                var approvalFailure = MapApprovalGateFailure(approvalGate);
                if (approvalFailure != null)
                    return approvalFailure;

                return await CreateStornoReversalAsync(payment, formattedReason, userId, key, stornoReason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling payment {PaymentId}", paymentId);
                return new PaymentResult
                {
                    Success = false,
                    Message = "An error occurred while cancelling payment",
                    Errors = { ex.Message }
                };
            }
        }

        public async Task<PaymentDetails?> FindOriginalSalePaymentByReceiptNumberAsync(Guid cashRegisterId, string receiptNumber)
        {
            var key = receiptNumber.Trim();
            return await _context.PaymentDetails.AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.CashRegisterId == cashRegisterId &&
                    p.ReceiptNumber == key &&
                    p.IsActive &&
                    !p.IsStorno &&
                    !p.IsRefund &&
                    p.RksvSpecialReceiptKind == null);
        }

        public string FormatStornoReasonForAudit(StornoReason reason) =>
            reason switch
            {
                StornoReason.None => "None",
                StornoReason.FalscherBetrag => "Falscher Betrag",
                StornoReason.KundeStorniert => "Kunde storniert",
                StornoReason.TechnischerFehler => "Technischer Fehler",
                StornoReason.Anderes => "Anderes",
                _ => reason.ToString()
            };

        /// <summary>
        /// Creates a fiscal storno (reversal) record for the given payment. Original payment is never modified.
        /// Creates: storno PaymentDetails, credit note Invoice, Receipt, TSE signature; reverts stock.
        /// </summary>
        public async Task<PaymentResult> CreateStornoReversalAsync(
            PaymentDetails payment,
            string reason,
            string userId,
            string? cancelIdempotencyKey,
            StornoReason? stornoReasonEnum = null)
        {
            var paymentId = payment.Id;
            if (payment.IsStorno)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Cannot storno a storno payment",
                    Errors = { "The selected payment is already a reversal row; use the original sale payment." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_TARGET_IS_STORNO"
                };
            }

            if (!stornoReasonEnum.HasValue || stornoReasonEnum.Value == StornoReason.None)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Storno reason is required",
                    Errors = { "A valid StornoReason must be supplied for RKSV compliance." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_REASON_REQUIRED"
                };
            }

            var resolvedStornoReason = stornoReasonEnum.Value;

            if (!string.IsNullOrWhiteSpace(cancelIdempotencyKey))
            {
                var key = cancelIdempotencyKey.Trim();
                var existingByKey = await _context.PaymentDetails.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.CancelIdempotencyKey == key && p.IsStorno);
                if (existingByKey != null)
                {
                    if (!await PaymentBelongsToEffectiveTenantAsync(existingByKey))
                    {
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "Payment not found",
                            Errors = { "Payment not found" },
                            IsDeterministicFailure = true
                        };
                    }

                    if (existingByKey.OriginalPaymentId == paymentId)
                    {
                        _logger.LogInformation(
                            "Idempotent storno: returning existing reversal {StornoId} for payment {PaymentId} key {Key}",
                            existingByKey.Id,
                            paymentId,
                            key);
                        await TryMarkPreorderCancelledAsync(paymentId);
                        var replayRefundFailure = await RefundCardAfterFiscalStornoAsync(paymentId, payment.TotalAmount)
                            .ConfigureAwait(false);
                        if (replayRefundFailure != null)
                            return replayRefundFailure;
                        return new PaymentResult
                        {
                            Success = true,
                            Message = "Payment cancelled successfully",
                            Payment = existingByKey,
                            PaymentId = existingByKey.Id
                        };
                    }

                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Idempotency key already used for a different storno",
                        Errors = { "CancelIdempotencyKey is already bound to another reversal." },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "STORNO_IDEMPOTENCY_KEY_CONFLICT"
                    };
                }
            }

            var hasRefundRows = await _context.PaymentDetails.AsNoTracking()
                .AnyAsync(p => p.OriginalPaymentId == paymentId && p.IsRefund && p.IsActive);
            if (hasRefundRows)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Cannot storno: partial refunds already exist for this receipt",
                    Errors = { "A full storno is not allowed after partial refunds on the same sale." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_BLOCKED_BY_REFUNDS"
                };
            }

            var alreadyHasStorno = await _context.PaymentDetails.AsNoTracking()
                .AnyAsync(p => p.OriginalPaymentId == paymentId && p.IsStorno);
            if (alreadyHasStorno)
            {
                var refundFailure = await RefundCardAfterFiscalStornoAsync(paymentId, payment.TotalAmount)
                    .ConfigureAwait(false);
                if (refundFailure != null)
                    return refundFailure;

                return new PaymentResult
                {
                    Success = false,
                    Message = "Payment is already cancelled",
                    Errors = { "A reversal (storno) already exists for this payment" },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_ALREADY_EXISTS"
                };
            }

            if (payment.CashRegisterId == Guid.Empty)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Payment has no valid CashRegisterId; cannot create fiscal storno",
                    Errors = { "CashRegisterId is required for reversal" }
                };
            }

            var reg = await _context.CashRegisters.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == payment.CashRegisterId);
            if (reg == null)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Cash register for this payment no longer exists",
                    Errors = { "Cannot create storno: cash register was removed." }
                };
            }

            var cashRegisterId = payment.CashRegisterId;
            var registerNumber = reg.RegisterNumber;
            var originalReceipt = await _context.Receipts.AsNoTracking()
                .FirstOrDefaultAsync(r => r.PaymentId == paymentId);
            var originalInvoice = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.SourcePaymentId == paymentId);

            if (originalReceipt == null || originalReceipt.ReceiptId == Guid.Empty)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Original receipt is required for storno",
                    Errors = { "RKSV storno requires an existing receipt linked to the original payment." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_ORIGINAL_RECEIPT_REQUIRED"
                };
            }

            var originalItems = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.RootElement.GetRawText()) ?? new List<PaymentItem>();
            var stornoItems = originalItems.Select(i => new PaymentItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = -i.UnitPrice,
                TotalPrice = -i.TotalPrice,
                TaxType = i.TaxType,
                TaxRate = i.TaxRate,
                TaxAmount = -i.TaxAmount,
                LineNet = -i.LineNet,
                Modifiers = (i.Modifiers ?? new List<PaymentItemModifierSnapshot>()).Select(m => new PaymentItemModifierSnapshot
                {
                    ModifierId = m.ModifierId,
                    Name = m.Name,
                    UnitPrice = -m.UnitPrice,
                    TotalPrice = -m.TotalPrice,
                    TaxType = m.TaxType,
                    TaxRate = m.TaxRate,
                    TaxAmount = -m.TaxAmount,
                    LineNet = -m.LineNet
                }).ToList()
            }).ToList();

            var originalTaxDetails = new Dictionary<string, decimal>();
            try
            {
                if (payment.TaxDetails?.RootElement.ValueKind == JsonValueKind.Object)
                    originalTaxDetails = JsonSerializer.Deserialize<Dictionary<string, decimal>>(payment.TaxDetails.RootElement.GetRawText()) ?? new Dictionary<string, decimal>();
            }
            catch { /* keep empty */ }
            var stornoTaxDetails = originalTaxDetails.ToDictionary(kv => kv.Key, kv => -kv.Value);

            var companyProfile = await _companyProfileProvider.GetCompanyProfileAsync().ConfigureAwait(false);
            var countryBinding = await _countryStrategyContext.LoadAsync().ConfigureAwait(false);
            var stornoId = Guid.NewGuid();
            var companyAddress = $"{companyProfile.Street}, {companyProfile.ZipCode} {companyProfile.City}";
            string stornoBelegNr = string.Empty;
            Guid stornoInvoiceId = Guid.Empty;
            string signingProvider = "local";

            await using var dbTx = await _context.Database.BeginTransactionAsync();
            try
            {
                var effectiveTenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                stornoBelegNr = await AllocateCountryBelegNrAsync(countryBinding, dbTx, cashRegisterId, registerNumber);

                var storno = new PaymentDetails
                {
                    Id = stornoId,
                    CustomerId = payment.CustomerId,
                    CustomerName = payment.CustomerName,
                    PaymentItems = JsonDocument.Parse(JsonSerializer.Serialize(stornoItems)),
                    TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(stornoTaxDetails)),
                    OriginalPaymentId = paymentId,
                    OriginalReceiptId = originalReceipt.ReceiptId,
                    IsRefund = false,
                    IsStorno = true,
                    StornoReason = resolvedStornoReason,
                    CancellationReason = reason,
                    CancelledAt = DateTime.UtcNow,
                    TotalAmount = -payment.TotalAmount,
                    TaxAmount = -payment.TaxAmount,
                    PaymentMethod = payment.PaymentMethod,
                    Notes = $"Storno: {reason}",
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Steuernummer = payment.Steuernummer ?? string.Empty,
                    CashRegisterId = cashRegisterId,
                    CashierId = userId,
                    TableNumber = payment.TableNumber,
                    TseTimestamp = DateTime.UtcNow,
                    ReceiptNumber = stornoBelegNr,
                    CancelIdempotencyKey = cancelIdempotencyKey
                };
                CompanyProfileMapper.CopySnapshotFromOriginal(storno, payment, companyProfile);
                FiscalDocumentCountryStamp.Apply(storno, countryBinding);

                await RunPaymentHookAsync(
                    effectiveTenantId,
                    TenantExtensionHooks.PaymentBeforeSign,
                    storno.Id,
                    storno.TotalAmount,
                    stornoBelegNr);

                // Fiskaly SIGN AT: negative storno amount is signed as receipt_type=CANCELLATION
                // (PUT /cash-register/{id}/receipt/{id}) via TseService — same endpoint as the Fiskaly test page.
                TseSignatureResult sigResult;
                try
                {
                    sigResult = await FiscalTseSigning.SignAsync(
                        _tseService,
                        new FiscalSigningRequest(
                            cashRegisterId,
                            stornoBelegNr,
                            storno.TotalAmount,
                            registerNumber,
                            TaxDetailsJson: JsonSerializer.Serialize(stornoTaxDetails),
                            DbTransaction: dbTx));
                    signingProvider = sigResult.SigningProvider;
                }
                catch (Exception ex)
                {
                    await dbTx.RollbackAsync();
                    _logger.LogError(ex, "Failed to generate TSE signature for storno {StornoId}", stornoId);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Failed to generate TSE signature for reversal",
                        Errors = { "TSE signature generation failed" }
                    };
                }

                storno.TseSignature = sigResult.CompactJws;
                storno.PrevSignatureValueUsed = sigResult.PrevSignatureValueUsed;
                storno.CertificateThumbprint = sigResult.CertificateThumbprint;
                _logger.LogInformation("TSE signature generated for storno {StornoId} BelegNr {BelegNr}", stornoId, stornoBelegNr);

                stornoInvoiceId = Guid.NewGuid();
                var stornoInvoice = new Invoice
                {
                    Id = stornoInvoiceId,
                    SourcePaymentId = storno.Id,
                    InvoiceNumber = storno.ReceiptNumber,
                    InvoiceDate = storno.CreatedAt,
                    DueDate = storno.CreatedAt,
                    Status = InvoiceStatus.Paid,
                    Subtotal = storno.TotalAmount - storno.TaxAmount,
                    TaxAmount = storno.TaxAmount,
                    TotalAmount = storno.TotalAmount,
                    PaidAmount = storno.TotalAmount,
                    RemainingAmount = 0,
                    CustomerName = storno.CustomerName,
                    CustomerTaxNumber = payment.Steuernummer,
                    CompanyName = storno.CompanyName ?? companyProfile.CompanyName,
                    CompanyTaxNumber = storno.Steuernummer,
                    CompanyAddress = storno.CompanyAddress ?? companyAddress,
                    TseSignature = storno.TseSignature ?? string.Empty,
                    KassenId = registerNumber,
                    TseTimestamp = storno.TseTimestamp,
                    CashRegisterId = cashRegisterId,
                    PaymentMethod = payment.PaymentMethod,
                    PaymentReference = null,
                    PaymentDate = storno.CreatedAt,
                    InvoiceItems = storno.PaymentItems,
                    TaxDetails = storno.TaxDetails,
                    DocumentType = DocumentType.CreditNote,
                    OriginalInvoiceId = originalInvoice?.Id,
                    StornoReasonCode = resolvedStornoReason.ToString(),
                    StornoReasonText = reason,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    IsActive = true
                };
                FiscalDocumentCountryStamp.CopyFromPayment(stornoInvoice, storno, countryBinding);

                _context.PaymentDetails.Add(storno);
                _context.Invoices.Add(stornoInvoice);
                await _receiptService.AddReceiptFromPaymentToContextAsync(storno);

                var stornoReceiptEntity = _context.ChangeTracker.Entries<Receipt>()
                    .Select(e => e.Entity)
                    .FirstOrDefault(r => r.PaymentId == stornoId);
                if (stornoReceiptEntity == null)
                {
                    await dbTx.RollbackAsync();
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Internal error: storno receipt missing.",
                        Errors = { "Cannot finalize reversal without receipt context." }
                    };
                }

                await _voucherLedger.ApplyVoucherRefundsForStornoAsync(
                    paymentId,
                    stornoId,
                    stornoReceiptEntity.ReceiptId,
                    effectiveTenantId,
                    userId,
                    cancelIdempotencyKey,
                    CancellationToken.None);

                foreach (var item in originalItems)
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId && p.TenantId == effectiveTenantId);
                    if (product != null && _inventoryOptions.EnforceStockOnSales)
                    {
                        product.StockQuantity += item.Quantity;
                        product.UpdatedAt = DateTime.UtcNow;
                    }
                    else if (product != null && !_inventoryOptions.EnforceStockOnSales)
                    {
                        _logger.LogDebug(
                            "Stock enforcement disabled: storno skipped stock revert for product {ProductId} qty={Qty}",
                            item.ProductId,
                            item.Quantity);
                    }
                }

                await _context.SaveChangesAsync();
                await dbTx.CommitAsync();
            }
            catch (DbUpdateException ex) when (IsStornoUniqueViolation(ex))
            {
                await dbTx.RollbackAsync();
                _logger.LogWarning(ex, "Concurrent storno lost the unique original-payment race for {PaymentId}", paymentId);
                return new PaymentResult
                {
                    Success = false,
                    Message = "Payment is already cancelled",
                    Errors = { "A reversal (storno) already exists for this payment" },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_ALREADY_EXISTS"
                };
            }
            catch (DbUpdateException ex) when (IsCancelIdempotencyKeyViolation(ex))
            {
                await dbTx.RollbackAsync();
                if (cancelIdempotencyKey != null)
                {
                    var existing = await _context.PaymentDetails.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.CancelIdempotencyKey == cancelIdempotencyKey && p.IsStorno);
                    if (existing != null)
                    {
                        if (!await PaymentBelongsToEffectiveTenantAsync(existing))
                        {
                            return new PaymentResult
                            {
                                Success = false,
                                Message = "Payment not found",
                                Errors = { "Payment not found" }
                            };
                        }
                        _logger.LogInformation("Idempotent cancel (race): returning existing storno {StornoId} for key {Key}", existing.Id, cancelIdempotencyKey);
                        await TryMarkPreorderCancelledAsync(paymentId);
                        return new PaymentResult
                        {
                            Success = true,
                            Message = "Payment cancelled successfully",
                            Payment = existing
                        };
                    }
                }
                throw;
            }
            catch (ExtensionFailedException ex)
            {
                await dbTx.RollbackAsync();
                _logger.LogWarning(ex, "Tenant extension {Feature} rejected storno for payment {PaymentId}", ex.FeatureName, paymentId);
                return ExtensionRejected(ex);
            }
            catch (Exception ex)
            {
                await dbTx.RollbackAsync();
                _logger.LogError(ex, "Storno fiscal transaction failed for payment {PaymentId}", paymentId);
                return new PaymentResult
                {
                    Success = false,
                    Message = "Storno transaction failed",
                    Errors = { ex.Message }
                };
            }

            var cardRefundFailure = await RefundCardAfterFiscalStornoAsync(paymentId, payment.TotalAmount)
                .ConfigureAwait(false);
            if (cardRefundFailure != null)
            {
                cardRefundFailure.PaymentId = stornoId;
                return cardRefundFailure;
            }

            var stornoRow = await _context.PaymentDetails.AsNoTracking()
                .FirstAsync(p => p.Id == stornoId);

            _logger.LogInformation("Storno created for payment {PaymentId} by user {UserId} (BelegNr {BelegNr}, Invoice {InvoiceId}). Original payment unchanged.",
                paymentId, userId, stornoBelegNr, stornoInvoiceId);

            await LogPaymentAuditAsync("PaymentReversal", "Payment", stornoRow.Id, userId,
                amount: stornoRow.TotalAmount,
                paymentMethod: stornoRow.PaymentMethodRaw,
                tseSignature: stornoRow.TseSignature,
                description: reason,
                responseData: new
                {
                    stornoRow.Id,
                    stornoRow.ReceiptNumber,
                    stornoRow.OriginalPaymentId,
                    stornoRow.OriginalReceiptId,
                    stornoRow.CancellationReason,
                    CreatedAt = stornoRow.CreatedAt,
                    FiskalyReceiptType = FiskalyReceiptSchemaMapper.ReceiptTypeCancellation,
                    SigningProvider = signingProvider
                });

            await LogFiskalyCancellationAuditAsync(
                stornoRow,
                userId,
                signingProvider,
                reason);

            await TryMarkPreorderCancelledAsync(paymentId);
            return new PaymentResult
            {
                Success = true,
                Message = "Payment cancelled successfully",
                Payment = stornoRow
            };
        }

        /// <summary>
        /// Card gateway refund runs only after the fiscal storno row has committed.
        /// Cash sales have no card row and return success without a gateway call.
        /// </summary>
        private async Task<PaymentResult?> RefundCardAfterFiscalStornoAsync(Guid originalPaymentId, decimal amount)
        {
            if (_cardPaymentService == null)
                return null;

            var (gwOk, gwCode, gwMsg) = await _cardPaymentService
                .RefundForFiscalPaymentAsync(originalPaymentId, amount)
                .ConfigureAwait(false);
            if (gwOk)
                return null;

            return new PaymentResult
            {
                Success = false,
                Message = gwMsg ?? "Gateway refund failed.",
                Errors = { gwMsg ?? "Gateway refund failed." },
                IsDeterministicFailure = true,
                DiagnosticCode = gwCode ?? OnlinePaymentErrorCodes.GatewayRefundFailed
            };
        }
    }
}
