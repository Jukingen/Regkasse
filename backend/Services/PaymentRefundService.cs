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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KasseAPI_Final.Services
{
    public interface IPaymentRefundService
    {
        /// <summary>
        /// Ödeme iade et. Sprint 6: optional idempotencyKey — retries with same key return existing refund (no duplicate BelegNr/stock).
        /// </summary>
        Task<PaymentResult> RefundPaymentAsync(
            Guid paymentId,
            decimal amount,
            string reason,
            string userId,
            string? idempotencyKey = null,
            RefundReasonCode reasonCode = RefundReasonCode.Other,
            string? approvalToken = null);
    }

    /// <summary>
    /// Partial refund. Gateway refund runs before the fiscal transaction.
    /// Sales that redeemed a voucher (pure voucher or mixed cash + voucher) are refused with
    /// <c>VOUCHER_PARTIAL_REFUND_NOT_SUPPORTED</c>: this path never restores stored-value balance, so only full storno is allowed.
    /// </summary>
    public sealed class PaymentRefundService : IPaymentRefundService
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
        private readonly IPaymentReversalApprovalService _reversalApproval;
        private readonly ICardPaymentService? _cardPaymentService;
        private readonly ILogger _logger;
        private readonly PaymentTenantMatch _belongsToTenant;
        private readonly AllocateCountryBelegNr _allocateBelegNr;
        private readonly RunPaymentHook _runPaymentHook;
        private readonly LogPaymentAudit _logPaymentAudit;

        internal PaymentRefundService(
            AppDbContext context,
            IGenericRepository<PaymentDetails> paymentRepository,
            IUserService userService,
            IReceiptService receiptService,
            ICompanyProfileProvider companyProfileProvider,
            ISettingsTenantResolver settingsTenantResolver,
            ICountryStrategyContext countryStrategyContext,
            InventoryOptions inventoryOptions,
            ITseService tseService,
            IPaymentReversalApprovalService reversalApproval,
            ILogger logger,
            ICardPaymentService? cardPaymentService,
            PaymentTenantMatch belongsToTenant,
            AllocateCountryBelegNr allocateBelegNr,
            RunPaymentHook runPaymentHook,
            LogPaymentAudit logPaymentAudit)
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
            _reversalApproval = reversalApproval;
            _logger = logger;
            _cardPaymentService = cardPaymentService;
            _belongsToTenant = belongsToTenant;
            _allocateBelegNr = allocateBelegNr;
            _runPaymentHook = runPaymentHook;
            _logPaymentAudit = logPaymentAudit;
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

        private static PaymentResult? MapApprovalGateFailure(PaymentReversalApprovalGateOutcome gate) =>
            PaymentService.MapApprovalGateFailure(gate);

        private static PaymentResult ExtensionRejected(ExtensionFailedException ex) =>
            PaymentService.ExtensionRejected(ex);

        private static bool IsIdempotencyKeyViolation(DbUpdateException ex) =>
            PaymentCommitFailureMapper.IsIdempotencyKeyViolation(ex);



        /// <summary>
        /// Ödeme iade et. Sprint 6: optional idempotencyKey — retries with same key return existing refund (no duplicate BelegNr/stock).
        /// </summary>
        public async Task<PaymentResult> RefundPaymentAsync(
            Guid paymentId,
            decimal amount,
            string reason,
            string userId,
            string? idempotencyKey = null,
            RefundReasonCode reasonCode = RefundReasonCode.Other,
            string? approvalToken = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Refund reason is required (minimum 5 characters)",
                        Errors = { "Reason must be at least 5 characters" },
                        DiagnosticCode = "REFUND_REASON_REQUIRED",
                        IsDeterministicFailure = true
                    };
                }

                // Demo kullanıcı kontrolü: IsDemo bayrağı (ve eski Demo rolü geriye dönük uyumluluk için)
                var user = await _userService.GetUserByIdAsync(userId);
                if (DemoUserHelper.IsDemoUser(user))
                {
                    var rejectionReason = DemoUserHelper.GetDemoRejectionReason(user) ?? "DEMO_UNKNOWN";
                    _logger.LogWarning(
                        "Payment refund demo rejection: AuthenticatedUserId={AuthenticatedUserId} AuthenticatedUserEmail={AuthenticatedUserEmail} ResolvedUserId={ResolvedUserId} ResolvedUserEmail={ResolvedUserEmail} ResolvedUserRole={ResolvedUserRole} ResolvedUserIsDemo={ResolvedUserIsDemo} RejectionCode={RejectionCode} PaymentId={PaymentId}",
                        userId,
                        user?.Email ?? "",
                        user?.Id ?? "",
                        user?.Email ?? "",
                        user?.Role ?? "",
                        user?.IsDemo ?? false,
                        rejectionReason,
                        paymentId);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Demo users cannot refund real payments",
                        Errors = { "Demo users are restricted to test operations only" },
                        DiagnosticCode = rejectionReason
                    };
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

                // Sprint 6: Idempotency — if key provided and we already have a refund for this payment with this key, return it.
                var refundKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
                if (refundKey != null)
                {
                    var existingRefund = await _context.PaymentDetails.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdempotencyKey == refundKey && p.IsRefund && p.OriginalPaymentId == paymentId);
                    if (existingRefund != null)
                    {
                        if (!await PaymentBelongsToEffectiveTenantAsync(existingRefund))
                        {
                            return new PaymentResult
                            {
                                Success = false,
                                Message = "Payment not found",
                                Errors = { "Payment not found" }
                            };
                        }
                        _logger.LogInformation("Idempotent refund: returning existing refund {RefundId} for payment {PaymentId} key {Key}", existingRefund.Id, paymentId, refundKey);
                        return new PaymentResult
                        {
                            Success = true,
                            Message = "Refund processed successfully",
                            Payment = existingRefund
                        };
                    }
                }

                // Already cancelled: legacy (IsActive=false) or fiscal storno exists. Do not refund.
                var hasStorno = await _context.PaymentDetails.AsNoTracking()
                    .AnyAsync(p => p.OriginalPaymentId == paymentId && p.IsStorno);
                if (!payment.IsActive || hasStorno)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Cannot refund cancelled payment",
                        Errors = { "Payment has been cancelled (or has a reversal) and cannot be refunded" }
                    };
                }

                const decimal refundTol = 0.01m;
                if (amount <= 0)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Refund amount must be greater than zero",
                        Errors = { "Refund amount must be greater than zero" },
                        IsDeterministicFailure = true
                    };
                }

                var refundedSoFar = await _context.PaymentDetails.AsNoTracking()
                    .Where(p => p.OriginalPaymentId == paymentId && p.IsRefund && p.IsActive)
                    .SumAsync(p => (decimal?)-p.TotalAmount) ?? 0m;

                var remainingRefundable = payment.TotalAmount - refundedSoFar;
                if (amount > remainingRefundable + refundTol)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Refund amount exceeds remaining refundable amount",
                        Errors = { $"Refund amount exceeds remaining refundable amount (remaining={remainingRefundable:N2})." },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "REFUND_EXCEEDS_REMAINING"
                    };
                }

                // Voucher-paid sale (pure voucher or mixed cash + voucher): this refund path never restores
                // stored-value balance, in any amount. Refuse before approval gate and gateway refund; full storno only.
                // Detection mirrors the storno path (Redeem ledger rows of the original payment) plus the voucher method.
                var paidWithVoucher =
                    string.Equals(payment.PaymentMethodRaw, ((int)PaymentMethod.Voucher).ToString(), StringComparison.Ordinal)
                    || await _context.VoucherLedgerEntries.AsNoTracking()
                        .AnyAsync(l => l.PaymentId == paymentId && l.Type == VoucherTransactionType.Redeem);
                if (paidWithVoucher)
                {
                    _logger.LogWarning(
                        "Refund refused for voucher-paid sale (full storno only). OriginalPaymentId={PaymentId}",
                        paymentId);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Voucher-paid sales support full storno only; a refund is not supported.",
                        Errors = { "Voucher-paid sales support full storno only; a refund is not supported." },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "VOUCHER_PARTIAL_REFUND_NOT_SUPPORTED"
                    };
                }

                var formattedRefundReason = PaymentReversalReasonMapper.FormatRefundReason(reasonCode, reason);
                var approvalGate = await _reversalApproval.EnforceApprovalAsync(
                    payment,
                    PaymentReversalOperation.Refund,
                    amount,
                    formattedRefundReason,
                    (int)reasonCode,
                    userId,
                    approvalToken,
                    refundKey,
                    CancellationToken.None);
                var approvalFailure = MapApprovalGateFailure(approvalGate);
                if (approvalFailure != null)
                    return approvalFailure;

                decimal refundRatio = amount / payment.TotalAmount;
                decimal refundTaxAmount = -payment.TaxAmount * refundRatio;

                if (payment.CashRegisterId == Guid.Empty)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment has no valid CashRegisterId",
                        Errors = { "Cannot refund without cash register context." }
                    };
                }

                var refundReg = await _context.CashRegisters.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == payment.CashRegisterId);
                if (refundReg == null)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Cash register for this payment no longer exists",
                        Errors = { "Cannot create refund: cash register was removed." }
                    };
                }

                var refundCashRegisterId = payment.CashRegisterId;
                var refundRegisterNumber = refundReg.RegisterNumber;
                var originalSaleReceipt = await _context.Receipts.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.PaymentId == paymentId);
                var originalInvoiceForRefund = await _context.Invoices.AsNoTracking()
                    .FirstOrDefaultAsync(i => i.SourcePaymentId == paymentId);

                if (_cardPaymentService != null)
                {
                    var (gwOk, gwCode, gwMsg) = await _cardPaymentService
                        .RefundForFiscalPaymentAsync(paymentId, amount)
                        .ConfigureAwait(false);
                    if (!gwOk)
                    {
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

                // Build negated PaymentItems so Receipt totals match (full or partial refund)
                var originalItems = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.RootElement.GetRawText()) ?? new List<PaymentItem>();
                var refundItems = originalItems.Select(i =>
                {
                    var scale = refundRatio;
                    return new PaymentItem
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        UnitPrice = -i.UnitPrice * scale,
                        TotalPrice = -i.TotalPrice * scale,
                        TaxType = i.TaxType,
                        TaxRate = i.TaxRate,
                        TaxAmount = -i.TaxAmount * scale,
                        LineNet = -i.LineNet * scale,
                        Modifiers = (i.Modifiers ?? new List<PaymentItemModifierSnapshot>()).Select(m => new PaymentItemModifierSnapshot
                        {
                            ModifierId = m.ModifierId,
                            Name = m.Name,
                            UnitPrice = -m.UnitPrice * scale,
                            TotalPrice = -m.TotalPrice * scale,
                            TaxType = m.TaxType,
                            TaxRate = m.TaxRate,
                            TaxAmount = -m.TaxAmount * scale,
                            LineNet = -m.LineNet * scale
                        }).ToList()
                    };
                }).ToList();

                // Negated tax details for refund
                var originalTaxDetails = new Dictionary<string, decimal>();
                try
                {
                    if (payment.TaxDetails?.RootElement.ValueKind == JsonValueKind.Object)
                        originalTaxDetails = JsonSerializer.Deserialize<Dictionary<string, decimal>>(payment.TaxDetails.RootElement.GetRawText()) ?? new Dictionary<string, decimal>();
                }
                catch { /* keep empty */ }
                var refundTaxDetails = originalTaxDetails.ToDictionary(kv => kv.Key, kv => -kv.Value * refundRatio);

                var companyProfile = await _companyProfileProvider.GetCompanyProfileAsync().ConfigureAwait(false);
                var countryBinding = await _countryStrategyContext.LoadAsync().ConfigureAwait(false);
                var refundId = Guid.NewGuid();
                var companyAddress = $"{companyProfile.Street}, {companyProfile.ZipCode} {companyProfile.City}";
                string refundBelegNr = string.Empty;
                Guid refundInvoiceId = Guid.Empty;

                await using var refundTx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var effectiveTenantIdForRefund = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
                    refundBelegNr = await AllocateCountryBelegNrAsync(countryBinding, refundTx, refundCashRegisterId, refundRegisterNumber);

                    var refund = new PaymentDetails
                    {
                        Id = refundId,
                        CustomerId = payment.CustomerId,
                        CustomerName = payment.CustomerName,
                        PaymentItems = JsonDocument.Parse(JsonSerializer.Serialize(refundItems)),
                        TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(refundTaxDetails)),
                        OriginalPaymentId = paymentId,
                        OriginalReceiptId = originalSaleReceipt?.ReceiptId,
                        IsRefund = true,
                        RefundReason = formattedRefundReason,
                        RefundAmount = amount,
                        RefundedAt = DateTime.UtcNow,
                        TotalAmount = -amount,
                        TaxAmount = refundTaxAmount,
                        PaymentMethod = payment.PaymentMethod,
                        Notes = $"Refund: {reason}",
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        Steuernummer = payment.Steuernummer ?? string.Empty,
                        CashRegisterId = refundCashRegisterId,
                        CashierId = userId,
                        TableNumber = payment.TableNumber,
                        TseTimestamp = DateTime.UtcNow,
                        ReceiptNumber = refundBelegNr,
                        IdempotencyKey = refundKey
                    };
                    CompanyProfileMapper.CopySnapshotFromOriginal(refund, payment, companyProfile);
                    FiscalDocumentCountryStamp.Apply(refund, countryBinding);

                    await RunPaymentHookAsync(
                        effectiveTenantIdForRefund,
                        TenantExtensionHooks.PaymentBeforeSign,
                        refund.Id,
                        refund.TotalAmount,
                        refundBelegNr);

                    TseSignatureResult sigResult;
                    try
                    {
                        sigResult = await FiscalTseSigning.SignAsync(
                            _tseService,
                            new FiscalSigningRequest(
                                refundCashRegisterId,
                                refundBelegNr,
                                refund.TotalAmount,
                                refundRegisterNumber,
                                TaxDetailsJson: JsonSerializer.Serialize(refundTaxDetails),
                                DbTransaction: refundTx));
                    }
                    catch (Exception ex)
                    {
                        await refundTx.RollbackAsync();
                        _logger.LogError(ex, "Failed to generate TSE signature for refund {RefundId}", refundId);
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "Failed to generate TSE signature for refund",
                            Errors = { "TSE signature generation failed" }
                        };
                    }

                    refund.TseSignature = sigResult.CompactJws;
                    refund.PrevSignatureValueUsed = sigResult.PrevSignatureValueUsed;
                    refund.CertificateThumbprint = sigResult.CertificateThumbprint;
                    _logger.LogInformation("TSE signature generated for refund {RefundId} BelegNr {BelegNr}", refundId, refundBelegNr);

                    refundInvoiceId = Guid.NewGuid();
                    var refundInvoice = new Invoice
                    {
                        Id = refundInvoiceId,
                        SourcePaymentId = refund.Id,
                        InvoiceNumber = refund.ReceiptNumber,
                        InvoiceDate = refund.CreatedAt,
                        DueDate = refund.CreatedAt,
                        Status = InvoiceStatus.Paid,
                        Subtotal = refund.TotalAmount - refund.TaxAmount,
                        TaxAmount = refund.TaxAmount,
                        TotalAmount = refund.TotalAmount,
                        PaidAmount = refund.TotalAmount,
                        RemainingAmount = 0,
                        CustomerName = refund.CustomerName,
                        CustomerTaxNumber = payment.Steuernummer,
                        CompanyName = refund.CompanyName ?? companyProfile.CompanyName,
                        CompanyTaxNumber = refund.Steuernummer,
                        CompanyAddress = refund.CompanyAddress ?? companyAddress,
                        TseSignature = refund.TseSignature ?? string.Empty,
                        KassenId = refundRegisterNumber,
                        TseTimestamp = refund.TseTimestamp,
                        CashRegisterId = refundCashRegisterId,
                        PaymentMethod = payment.PaymentMethod,
                        PaymentReference = null,
                        PaymentDate = refund.CreatedAt,
                        InvoiceItems = refund.PaymentItems,
                        TaxDetails = refund.TaxDetails,
                        DocumentType = DocumentType.CreditNote,
                        OriginalInvoiceId = originalInvoiceForRefund?.Id,
                        StornoReasonText = reason,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userId,
                        IsActive = true
                    };
                    FiscalDocumentCountryStamp.CopyFromPayment(refundInvoice, refund, countryBinding);

                    _context.PaymentDetails.Add(refund);
                    _context.Invoices.Add(refundInvoice);
                    await _receiptService.AddReceiptFromPaymentToContextAsync(refund);

                    foreach (var item in originalItems)
                    {
                        var product = await _context.Products
                            .FirstOrDefaultAsync(p => p.Id == item.ProductId && p.TenantId == effectiveTenantIdForRefund);
                        if (product != null)
                        {
                            var refundQuantity = (int)Math.Round(item.Quantity * refundRatio);
                            if (refundQuantity > 0)
                            {
                                if (_inventoryOptions.EnforceStockOnSales)
                                {
                                    product.StockQuantity += refundQuantity;
                                    product.UpdatedAt = DateTime.UtcNow;
                                }
                                else
                                {
                                    _logger.LogDebug(
                                        "Stock enforcement disabled: refund skipped stock revert for product {ProductId} qty={Qty}",
                                        item.ProductId,
                                        refundQuantity);
                                }
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await refundTx.CommitAsync();
                }
                catch (DbUpdateException ex) when (IsIdempotencyKeyViolation(ex))
                {
                    await refundTx.RollbackAsync();
                    if (refundKey != null)
                    {
                        var existing = await _context.PaymentDetails.AsNoTracking()
                            .FirstOrDefaultAsync(p => p.IdempotencyKey == refundKey && p.IsRefund && p.OriginalPaymentId == paymentId);
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
                            _logger.LogInformation("Idempotent refund (race): returning existing refund {RefundId} for key {Key}", existing.Id, refundKey);
                            return new PaymentResult
                            {
                                Success = true,
                                Message = "Refund processed successfully",
                                Payment = existing
                            };
                        }
                    }
                    throw;
                }
                catch (ExtensionFailedException ex)
                {
                    await refundTx.RollbackAsync();
                    _logger.LogWarning(ex, "Tenant extension {Feature} rejected refund for payment {PaymentId}", ex.FeatureName, paymentId);
                    return ExtensionRejected(ex);
                }
                catch (Exception ex)
                {
                    await refundTx.RollbackAsync();
                    _logger.LogError(ex, "Refund fiscal transaction failed for payment {PaymentId}", paymentId);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Refund transaction failed",
                        Errors = { ex.Message }
                    };
                }

                var refundRow = await _context.PaymentDetails.AsNoTracking()
                    .FirstAsync(p => p.Id == refundId);

                _logger.LogInformation("Refund created for payment {PaymentId} by user {UserId} for amount {Amount} (BelegNr {BelegNr}, Invoice {InvoiceId})",
                    paymentId, userId, amount, refundBelegNr, refundInvoiceId);

                await LogPaymentAuditAsync("PaymentRefunded", "Payment", refundRow.Id, userId,
                    amount: refundRow.TotalAmount,
                    paymentMethod: refundRow.PaymentMethodRaw,
                    tseSignature: refundRow.TseSignature,
                    description: reason,
                    responseData: new
                    {
                        refundRow.Id,
                        refundRow.ReceiptNumber,
                        refundRow.OriginalPaymentId,
                        refundRow.OriginalReceiptId,
                        RefundAmount = amount,
                        CreatedAt = refundRow.CreatedAt
                    });

                return new PaymentResult
                {
                    Success = true,
                    Message = "Refund processed successfully",
                    Payment = refundRow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund for payment {PaymentId}", paymentId);
                return new PaymentResult
                {
                    Success = false,
                    Message = "An error occurred while processing refund",
                    Errors = { ex.Message }
                };
            }
        }
    }
}
