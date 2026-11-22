using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KasseAPI_Final.Services
{
    public partial class PaymentService
    {
        /// <summary>
        /// Sale, storno, and refund entry. The fiscal transaction starts and commits here.
        /// Phases that run inside that transaction do not begin another transaction, commit, or call SaveChanges.
        /// </summary>
        private async Task<PaymentResult> CreatePaymentCoreAsync(
            CreatePaymentRequest request,
            string userId,
            Guid? offlineTransactionId = null,
            Guid? offlineReplayBatchCorrelationId = null)
        {
            // Lisans kontrolü: trial bitmiş ve geçerli ücretli lisans yoksa ödeme oluşturmayı engelle.
            // GET istekleri ve admin panel erişimi etkilenmez; yalnızca ödeme akışı bloke edilir.
            // Try bloğundan önce yapılır ki aşağıdaki genel catch (Exception) bloğu exception'ı yutmasın
            // ve LicenseExpiredException controller katmanına kadar yayılabilsin.
            var licenseCheckCancellation = _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
            await EnsureLicenseAllowsPaymentAsync(licenseCheckCancellation).ConfigureAwait(false);

            var preconditions = await _saleGate.ValidatePreconditionsAsync(request, licenseCheckCancellation).ConfigureAwait(false);
            if (preconditions.Stop != null)
                return preconditions.Stop;
            var gates = preconditions.Value!;

            try
            {
                var actor = await ResolveActorAndCustomerAsync(request, userId).ConfigureAwait(false);
                if (actor.Stop != null)
                    return actor.Stop;

                var reversal = await TryRedirectStornoOrRefundAsync(request, userId, actor.Value!).ConfigureAwait(false);
                if (reversal != null)
                    return reversal;

                var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
                var replay = await TryReplayIdempotentPaymentAsync(idempotencyKey, offlineTransactionId).ConfigureAwait(false);
                if (replay != null)
                    return replay;

                var prepared = await _saleGate.PrepareFiscalSaleAsync(request, userId, actor.Value!).ConfigureAwait(false);
                if (prepared.Stop != null)
                    return prepared.Stop;

                var sale = prepared.Value!;
                PaymentDetails? committedPayment = null;
                Invoice? committedInvoice = null;
                IReadOnlyList<IssuedTicketDto> committedIssuedTickets = [];
                var paymentStockLinesSkipped = 0;
                var effectiveTseRequired = sale.EffectiveTseRequired;

                // Product rows are read-only here. Stock changes stay inside the fiscal transaction,
                // after the register lock, so this lookup does not hold a pooled connection for the N+1 reads.
                var saleTenantId = await _settingsTenantResolver
                    .ResolveEffectiveTenantIdAsync(licenseCheckCancellation)
                    .ConfigureAwait(false);
                var productsById = await LoadSaleProductsAsync(saleTenantId, request.Items).ConfigureAwait(false);

                // Authoritative register id/number come from commit gate inside the fiscal transaction (row lock).
                // The signature-chain FOR UPDATE stays inside this transaction until Commit: releasing it earlier
                // would let two sales on one register sign from the same previous signature.
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var inTxReplay = await TryReplayIdempotencyInsideTransactionAsync(idempotencyKey).ConfigureAwait(false);
                    if (inTxReplay != null)
                    {
                        await transaction.RollbackAsync();
                        return inTxReplay;
                    }

                    var registerGate = await ValidateCommitRegisterAsync(userId, request, sale.Principal).ConfigureAwait(false);
                    if (registerGate.Stop != null)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return registerGate.Stop;
                    }

                    var cashRegisterId = registerGate.Value!.CashRegisterId;
                    var registerNumber = registerGate.Value.RegisterNumber;
                    var totals = await CalculateTotalsAsync(
                            request,
                            userId,
                            transaction,
                            sale,
                            cashRegisterId,
                            productsById,
                            saleTenantId)
                        .ConfigureAwait(false);
                    if (totals.Stop != null)
                    {
                        if (!totals.TransactionAlreadyRolledBack)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                        }

                        return totals.Stop;
                    }

                    var settlement = await _settlement.ResolveSettlementAsync(
                            request,
                            gates,
                            sale,
                            totals.Value!.TotalAmount,
                            totals.Value.EffectiveTenantId,
                            cashRegisterId,
                            offlineTransactionId,
                            licenseCheckCancellation)
                        .ConfigureAwait(false);
                    if (settlement.Stop != null)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return settlement.Stop;
                    }

                    effectiveTseRequired = settlement.Value!.EffectiveTseRequired;
                    paymentStockLinesSkipped = totals.Value!.PaymentStockLinesSkipped;
                    var docs = await _documents.PersistFiscalDocumentsAsync(
                            request,
                            userId,
                            transaction,
                            gates,
                            sale,
                            totals.Value,
                            settlement.Value,
                            cashRegisterId,
                            registerNumber,
                            idempotencyKey,
                            offlineTransactionId,
                            offlineReplayBatchCorrelationId)
                        .ConfigureAwait(false);
                    if (docs.Stop != null)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        return docs.Stop;
                    }

                    await _context.SaveChangesAsync();
                    await RunPaymentHookAsync(
                        docs.Value!.EffectiveTenantId,
                        TenantExtensionHooks.PaymentAfterPersist,
                        docs.Value.Payment.Id,
                        docs.Value.Payment.TotalAmount,
                        docs.Value.Payment.ReceiptNumber);
                    await transaction.CommitAsync();

                    committedPayment = docs.Value.Payment;
                    committedInvoice = docs.Value.Invoice;
                    committedIssuedTickets = docs.Value.IssuedTickets;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    var mapped = await _commitFailures.MapFiscalCommitFailureAsync(ex, idempotencyKey, request.CustomerId).ConfigureAwait(false);
                    if (mapped.Rethrow)
                        throw;
                    return mapped.Result!;
                }

                if (committedPayment != null && committedInvoice != null)
                {
                    return await _postCommit.DispatchPostCommitAsync(
                            request,
                            userId,
                            actor.Value!,
                            gates.TenantIdForFlags,
                            committedPayment,
                            committedInvoice,
                            committedIssuedTickets,
                            paymentStockLinesSkipped,
                            effectiveTseRequired,
                            offlineReplayBatchCorrelationId)
                        .ConfigureAwait(false);
                }

                throw new InvalidOperationException("Payment fiscal transaction finished without a committed payment result.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment for customer {CustomerId}", request.CustomerId);
                return new PaymentResult
                {
                    Success = false,
                    Message = "An error occurred while creating payment",
                    Errors = { ex.Message }
                };
            }
        }

        /// <summary>
        /// Rejects demo cashiers and loads the sale customer. No persistence.
        /// </summary>
        private async Task<PaymentCorePhase<Customer>> ResolveActorAndCustomerAsync(
            CreatePaymentRequest request,
            string userId)
        {
            PaymentActorConstraints.EnsurePrincipalDerivedActor(userId);

            _logger.LogInformation("Creating payment for customer {CustomerId} by user {UserId}", request.CustomerId, userId);

            // Demo gate uses authenticated user only (resolved by userId — same as JWT). Single resolved user.
            var user = await _userService.GetUserByIdAsync(userId);
            if (DemoUserHelper.IsDemoUser(user))
            {
                var rejectionReason = DemoUserHelper.GetDemoRejectionReason(user) ?? "DEMO_UNKNOWN";
                _logger.LogWarning(
                    "Payment demo rejection: AuthenticatedUserId={AuthenticatedUserId} AuthenticatedUserEmail={AuthenticatedUserEmail} ResolvedUserId={ResolvedUserId} ResolvedUserEmail={ResolvedUserEmail} ResolvedUserRole={ResolvedUserRole} ResolvedUserIsDemo={ResolvedUserIsDemo} RejectionCode={RejectionCode}",
                    userId,
                    user?.Email ?? "",
                    user?.Id ?? "",
                    user?.Email ?? "",
                    user?.Role ?? "",
                    user?.IsDemo ?? false,
                    rejectionReason);
                return PaymentCorePhase<Customer>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Demo users cannot create real payments",
                    Errors = { "Demo users are restricted to test operations only" },
                    DiagnosticCode = rejectionReason
                });
            }

            // Müşteri kontrolü
            var customer = await _customerRepository.GetByIdAsync(request.CustomerId);
            if (customer == null)
            {
                return PaymentCorePhase<Customer>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Customer not found",
                    Errors = { "Customer not found" },
                    IsDeterministicFailure = true
                });
            }

            var resolvedCustomerKind = CustomerKindResolver.ResolveFromCustomerId(customer.Id, request.CustomerKind);
            _logger.LogDebug(
                "Payment create resolved CustomerKind={CustomerKind} for CustomerId={CustomerId}",
                resolvedCustomerKind,
                customer.Id);
            return PaymentCorePhase<Customer>.Ok(customer);
        }

        /// <summary>
        /// Full storno and partial refund leave the sale path and return the reversal result.
        /// Null means this request is a sale and the caller should continue.
        /// </summary>
        private async Task<PaymentResult?> TryRedirectStornoOrRefundAsync(
            CreatePaymentRequest request,
            string userId,
            Customer customer)
        {
            if (!request.IsStorno && !request.IsRefund)
                return null;

            if (request.IsStorno && request.IsRefund)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Storno and refund are mutually exclusive",
                    Errors = { "IsStorno and IsRefund cannot both be true." },
                    IsDeterministicFailure = true
                };
            }

            if (request.CashRegisterId == Guid.Empty)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "CashRegisterId is required",
                    Errors = { "CashRegisterId must be a non-empty GUID referencing an authorized open cash register." },
                    DiagnosticCode = CashRegisterResolutionCodes.Required,
                    IsDeterministicFailure = true
                };
            }

            var principalStornoRefund = _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
            var registerValidationSr = await _cashRegisterResolution.ValidatePaymentRegisterAsync(
                userId,
                request.CashRegisterId,
                principalStornoRefund);
            if (!registerValidationSr.Ok)
            {
                _logger.LogWarning(
                    "Storno/refund payment create rejected: cash register policy. UserId={UserId} RegisterId={RegisterId} Code={Code}",
                    userId,
                    request.CashRegisterId,
                    registerValidationSr.Code);
                return new PaymentResult
                {
                    Success = false,
                    Message = registerValidationSr.Message,
                    Errors = { registerValidationSr.Message },
                    DiagnosticCode = MapCashRegisterDiagnosticToRksvPaymentCode(registerValidationSr.Code)
                };
            }

            var registerIdResolved = registerValidationSr.ResolvedRegisterId!.Value;
            var belegNr = request.OriginalReceiptNumber!.Trim();
            var originalSale = await _storno.FindOriginalSalePaymentByReceiptNumberAsync(registerIdResolved, belegNr);
            if (originalSale == null)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Original receipt not found",
                    Errors = { "No active sale payment matches OriginalReceiptNumber for this cash register." },
                    IsDeterministicFailure = true
                };
            }

            if (!await PaymentBelongsToEffectiveTenantAsync(originalSale))
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Original receipt not found",
                    Errors = { "No active sale payment matches OriginalReceiptNumber for this cash register." },
                    IsDeterministicFailure = true
                };
            }

            if (originalSale.CustomerId != request.CustomerId)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Customer does not match original receipt",
                    Errors = { "CustomerId must match the original sale for storno or refund." },
                    IsDeterministicFailure = true
                };
            }

            const decimal parityTol = 0.01m;
            if (request.IsStorno)
            {
                if (Math.Abs(request.TotalAmount - originalSale.TotalAmount) > parityTol)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Storno total does not match original receipt",
                        Errors = { "TotalAmount must match the original sale gross total for a full storno." },
                        IsDeterministicFailure = true
                    };
                }

                var refundRowsExist = await _context.PaymentDetails.AsNoTracking()
                    .AnyAsync(p => p.OriginalPaymentId == originalSale.Id && p.IsRefund && p.IsActive);
                if (refundRowsExist)
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

                var auditReason = _storno.FormatStornoReasonForAudit(request.StornoReason!.Value);
                return await _storno.CreateStornoReversalAsync(
                    originalSale,
                    auditReason,
                    userId,
                    string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
                    request.StornoReason);
            }

            var refundNotes = string.IsNullOrWhiteSpace(request.Notes) ? "Refund (POS create)" : request.Notes.Trim();
            return await RefundPaymentAsync(
                originalSale.Id,
                decimal.Round(request.TotalAmount, 2, MidpointRounding.AwayFromZero),
                refundNotes,
                userId,
                string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim());
        }

        /// <summary>
        /// Returns the existing payment for a repeated idempotency key.
        /// The offline-link write is the same SaveChanges the sale path performed before BeginTransaction.
        /// </summary>
        private async Task<PaymentResult?> TryReplayIdempotentPaymentAsync(
            string? idempotencyKey,
            Guid? offlineTransactionId)
        {
            if (idempotencyKey == null)
                return null;

            var existing = await _context.PaymentDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);
            if (existing == null)
                return null;

            // If this payment is linked to a controlled offline intent and the record was previously created
            // without that link (idempotent replay), attach the forensic link without touching fiscal fields.
            if (offlineTransactionId != null && existing.OfflineTransactionId == null)
            {
                var tracked = await _context.PaymentDetails
                    .FirstOrDefaultAsync(p => p.Id == existing.Id);
                if (tracked != null && tracked.OfflineTransactionId == null)
                {
                    tracked.OfflineTransactionId = offlineTransactionId;
                    await _context.SaveChangesAsync();
                    existing = tracked;
                }
            }

            var invoiceExists = await _context.Invoices.AsNoTracking()
                .AnyAsync(i => i.SourcePaymentId == existing.Id);
            _logger.LogInformation("Idempotent payment request: returning existing payment {PaymentId} for key {Key}", existing.Id, idempotencyKey);
            var (qr, demo, provider) = await BuildQrPayloadAndFlagsAsync(existing, !string.IsNullOrEmpty(existing.TseSignature));
            return new PaymentResult
            {
                Success = true,
                Message = "Payment created successfully",
                Payment = existing,
                PaymentId = existing.Id,
                TseSignature = existing.TseSignature,
                QrPayload = qr,
                IsDemoFiscal = demo,
                TseProvider = provider,
                InvoicePersisted = invoiceExists,
                IdempotentReplay = true,
                TimeSyncWarning = existing.TimeSyncWarning
            };
        }

        /// <summary>
        /// Second idempotency read inside the open fiscal transaction. The caller rolls the transaction back
        /// without clearing the change tracker, matching the previous in-transaction replay.
        /// </summary>
        private async Task<PaymentResult?> TryReplayIdempotencyInsideTransactionAsync(string? idempotencyKey)
        {
            if (idempotencyKey == null)
                return null;

            var existingByKeyInTx = await _context.PaymentDetails.AsNoTracking()
                .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);
            if (existingByKeyInTx == null)
                return null;

            var invoiceExistsInTx = await _context.Invoices.AsNoTracking()
                .AnyAsync(i => i.SourcePaymentId == existingByKeyInTx.Id);
            _logger.LogInformation("Idempotency: returning existing payment {PaymentId} for key {Key}", existingByKeyInTx.Id, idempotencyKey);
            var (qrInTx, demoInTx, providerInTx) = await BuildQrPayloadAndFlagsAsync(existingByKeyInTx, !string.IsNullOrEmpty(existingByKeyInTx.TseSignature));
            return new PaymentResult
            {
                Success = true,
                Message = "Payment created successfully",
                Payment = existingByKeyInTx,
                PaymentId = existingByKeyInTx.Id,
                TseSignature = existingByKeyInTx.TseSignature,
                QrPayload = qrInTx,
                IsDemoFiscal = demoInTx,
                TseProvider = providerInTx,
                InvoicePersisted = invoiceExistsInTx,
                IdempotentReplay = true,
                TimeSyncWarning = existingByKeyInTx.TimeSyncWarning
            };
        }

        /// <summary>
        /// Commit-gate register lock. Does not roll back; the caller does.
        /// </summary>
        private async Task<PaymentCorePhase<CommitRegister>> ValidateCommitRegisterAsync(
            string userId,
            CreatePaymentRequest request,
            ClaimsPrincipal principal)
        {
            var registerCommitValidation = await _cashRegisterResolution.ValidatePaymentRegisterForCommitAsync(
                userId,
                request.CashRegisterId,
                principal);
            if (!registerCommitValidation.Ok)
            {
                _logger.LogWarning(
                    "Payment rejected at commit gate: cash register policy. UserId={UserId} RegisterId={RegisterId} Code={Code}",
                    userId,
                    request.CashRegisterId,
                    registerCommitValidation.Code);
                return PaymentCorePhase<CommitRegister>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = registerCommitValidation.Message,
                    Errors = { registerCommitValidation.Message },
                    DiagnosticCode = MapCashRegisterDiagnosticToRksvPaymentCode(registerCommitValidation.Code)
                });
            }

            return PaymentCorePhase<CommitRegister>.Ok(new CommitRegister
            {
                CashRegisterId = registerCommitValidation.ResolvedRegisterId!.Value,
                RegisterNumber = registerCommitValidation.RegisterNumber!,
            });
        }
    }
}
