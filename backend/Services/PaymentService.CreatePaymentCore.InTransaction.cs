using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Services.Imei;
using KasseAPI_Final.Services.Tickets;
using KasseAPI_Final.Services.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KasseAPI_Final.Services
{
    public partial class PaymentService
    {
        /// <summary>
        /// Cart claim, line pricing, tax, and benefit usage on the caller's transaction.
        /// Variant and IMEI helpers still roll that transaction back themselves; other failures do not.
        /// </summary>
        private async Task<PaymentCorePhase<SaleTotals>> CalculateTotalsAsync(
            CreatePaymentRequest request,
            string userId,
            IDbContextTransaction transaction,
            PreparedFiscalSale sale,
            Guid cashRegisterId)
        {
            var effectiveTenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();

            // Ürün kontrolü ve stok güncelleme. Tek hesap motoru: CartMoneyHelper (gross model).
            var paymentItems = new List<PaymentItem>();
            var productIdToCategoryId = new Dictionary<Guid, Guid>();
            var paymentStockLinesSkipped = 0;

            var cartSnapshot = await TryGetCartSnapshotUnitPricesAsync(userId, request.TableNumber, request.Items);
            var cartSnapshotPrices = cartSnapshot?.UnitPrices;
            var cartClaim = await TryClaimMatchingCartForPaymentAsync(userId, request.TableNumber, request.Items)
                .ConfigureAwait(false);
            if (cartClaim != null)
                return PaymentCorePhase<SaleTotals>.Fail(cartClaim);

            var pricedLines = new List<(PaymentItemRequest Item, Product Product, decimal UnitGross)>();
            var pendingImeiRows = new List<ProductImei>();
            var reservedImeiCodes = new HashSet<string>(StringComparer.Ordinal);
            var variantPriceDeltas = new Dictionary<PaymentItemRequest, decimal>();
            foreach (var itemRequest in request.Items)
            {
                var product = await _context.Products
                    .Include(p => p.CategoryNavigation)
                    .FirstOrDefaultAsync(p => p.Id == itemRequest.ProductId && p.TenantId == effectiveTenantId);
                if (product == null)
                {
                    return PaymentCorePhase<SaleTotals>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Product not found",
                        Errors = { $"Product with ID {itemRequest.ProductId} not found" },
                        IsDeterministicFailure = true
                    });
                }

                if (product.CategoryNavigation == null)
                {
                    return PaymentCorePhase<SaleTotals>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Product category missing",
                        Errors = { $"Product {product.Name} has no category" }
                    });
                }

                if (!request.IsStorno && !request.IsRefund)
                {
                    var variantFailure = await TryApplyVariantForLineAsync(
                        product,
                        itemRequest,
                        variantPriceDeltas,
                        transaction,
                        userId).ConfigureAwait(false);
                    if (variantFailure != null)
                        return PaymentCorePhase<SaleTotals>.Fail(variantFailure, alreadyRolledBack: true);
                }

                // Phase 2: Sellable add-ons are product-only payment lines; no stock deduction (stok düşülmez).
                // Products with variants keep top-level stock unchanged; the variant row is the stock.
                // Stock is updated in memory on tracked entities; persisted in a single transaction with payment/invoice/receipt (no per-product UpdateAsync).
                if (!product.IsSellableAddOn && !variantPriceDeltas.ContainsKey(itemRequest))
                {
                    if (_inventoryOptions.EnforceStockOnSales)
                    {
                        if (product.StockQuantity < itemRequest.Quantity)
                        {
                            return PaymentCorePhase<SaleTotals>.Fail(new PaymentResult
                            {
                                Success = false,
                                Message = "Insufficient stock",
                                Errors = { $"Insufficient stock for product {product.Name}" }
                            });
                        }

                        product.StockQuantity -= itemRequest.Quantity;
                        product.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        paymentStockLinesSkipped++;
                        _logger.LogDebug(
                            "Stock enforcement disabled (Inventory:EnforceStockOnSales=false): no deduct for product {ProductId} name={ProductName} requestedQty={Qty} currentStock={Stock}",
                            product.Id,
                            product.Name,
                            itemRequest.Quantity,
                            product.StockQuantity);
                    }
                }

                if (!request.IsStorno && !request.IsRefund)
                {
                    var imeiFailure = await TryReserveImeisForLineAsync(
                        product,
                        itemRequest,
                        pendingImeiRows,
                        reservedImeiCodes,
                        transaction)
                        .ConfigureAwait(false);
                    if (imeiFailure != null)
                        return PaymentCorePhase<SaleTotals>.Fail(imeiFailure, alreadyRolledBack: true);
                }

                // VAT: country strategy (Austria delegates to CartMoneyHelper — same rounding).
                decimal unitGross;
                if (cartSnapshotPrices != null && cartSnapshotPrices.TryGetValue(product.Id, out var snapGross))
                {
                    unitGross = snapGross;
                }
                else
                {
                    var priceRes = await _pricingRuleResolver.ResolveUnitGrossAsync(
                        product.Price,
                        product.Id,
                        product.CategoryId,
                        cashRegisterId,
                        DateTime.UtcNow);
                    unitGross = priceRes.UnitPriceGross;
                }

                if (variantPriceDeltas.TryGetValue(itemRequest, out var priceDelta))
                {
                    unitGross = decimal.Round(unitGross + priceDelta, 2, MidpointRounding.AwayFromZero);
                    if (unitGross < 0)
                    {
                        return PaymentCorePhase<SaleTotals>.Fail(VariantPaymentFailure(
                            ProductVariantSaleCodes.Invalid,
                            "Variant price delta makes the line total negative."));
                    }
                }

                pricedLines.Add((itemRequest, product, unitGross));
            }

            var rateCatalog = TaxTypesFor(sale.CountryBinding);
            var taxResult = sale.TaxStrategy.CalculateTax(
                pricedLines.ConvertAll(l =>
                    CountryPaymentTaxLineMapper.FromProductTaxType(
                        sale.CountryBinding.Profile,
                        l.UnitGross,
                        l.Item.Quantity,
                        l.Product.TaxType,
                        rateCatalog)),
                new TaxCalculationContext
                {
                    CountryProfile = sale.CountryBinding.Profile,
                    VatRegime = sale.CountryBinding.VatRegime,
                    TaxExempt = sale.CountryBinding.Settings.TaxExempt,
                    BuyerVatId = request.Steuernummer,
                    DestinationCountry = CountryPaymentTaxLineMapper.ResolveOssDestinationCountry(
                        sale.CountryBinding.VatRegime,
                        request.Steuernummer),
                    TenantId = sale.CountryBinding.Settings.TenantId,
                });

            for (var i = 0; i < pricedLines.Count; i++)
            {
                var (itemRequest, product, _) = pricedLines[i];
                var line = taxResult.Lines[i];
                paymentItems.Add(new PaymentItem
                {
                    ProductId = product.Id,
                    ProductName = cartSnapshot != null
                        && cartSnapshot.StayNames.TryGetValue(product.Id, out var stayName)
                        ? stayName
                        : product.Name,
                    Quantity = itemRequest.Quantity,
                    UnitPrice = line.UnitPriceGross,
                    TotalPrice = line.LineGross,
                    TaxType = line.TaxType,
                    TaxRate = line.TaxRate,
                    TaxAmount = line.LineTax,
                    LineNet = line.LineNet
                });
                productIdToCategoryId[product.Id] = product.CategoryId;
            }

            var totalAmount = taxResult.Totals.TotalGross;
            var totalTaxAmount = taxResult.Totals.TotalVat;
            var taxDetails = taxResult.TaxDetails.ToDictionary(static kv => kv.Key, static kv => kv.Value);

            var benefitResult = await _benefits.CalculateBenefitsAsync(
                sale.Customer,
                paymentItems,
                productIdToCategoryId,
                totalAmount,
                totalTaxAmount,
                taxDetails);

            totalAmount = benefitResult.TotalAmount;
            totalTaxAmount = benefitResult.TotalTaxAmount;
            taxDetails = benefitResult.TaxDetails;
            JsonDocument? appliedBenefitsSnapshot = benefitResult.AppliedBenefitsSnapshot;

            if (benefitResult.UsageDeltas.Count > 0)
            {
                await _benefits.ApplyBenefitUsageMutationsAsync(benefitResult.UsageDeltas);
            }

            // Fiscal total authority: `totalAmount` is computed from catalog lines (and benefits); `request.TotalAmount` is only a client parity probe — persisted rows use `totalAmount`.
            const decimal amountTolerance = 0.01m;
            if (Math.Abs(totalAmount - request.TotalAmount) > amountTolerance)
            {
                _logger.LogWarning(
                    "Payment total mismatch: calculated={Calculated}, request={Request}. Rejecting for fiscal integrity.",
                    totalAmount, request.TotalAmount);
                return PaymentCorePhase<SaleTotals>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = "Total amount mismatch between client and server calculation.",
                    Errors = { "Total amount mismatch between client and server calculation." }
                });
            }

            return PaymentCorePhase<SaleTotals>.Ok(new SaleTotals
            {
                EffectiveTenantId = effectiveTenantId,
                PaymentItems = paymentItems,
                TotalAmount = totalAmount,
                TotalTaxAmount = totalTaxAmount,
                TaxDetails = taxDetails,
                AppliedBenefitsSnapshot = appliedBenefitsSnapshot,
                TaxResult = taxResult,
                PricedLines = pricedLines,
                PendingImeiRows = pendingImeiRows,
                PaymentStockLinesSkipped = paymentStockLinesSkipped,
                RateCatalog = rateCatalog,
            });
        }

        /// <summary>
        /// Pre-order guard, tender split, card intent, voucher plan, and NTP gate. Does not commit.
        /// </summary>
        private async Task<PaymentCorePhase<SaleSettlement>> ResolveSettlementAsync(
            CreatePaymentRequest request,
            PaymentPreconditions preconditions,
            PreparedFiscalSale sale,
            SaleTotals totals,
            Guid cashRegisterId,
            Guid? offlineTransactionId,
            CancellationToken licenseCheckCancellation)
        {
            var totalAmount = totals.TotalAmount;
            var effectiveTseRequired = sale.EffectiveTseRequired;
            if (request.PreorderBalanceOrderId is Guid preorderBalanceId && preorderBalanceId != Guid.Empty)
            {
                if (_preorderService is null)
                {
                    return PaymentCorePhase<SaleSettlement>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Pre-order balance payment is not available.",
                        Errors = { "Pre-order balance payment is not available." },
                        DiagnosticCode = "PREORDER_UNAVAILABLE"
                    });
                }

                if (request.IsPreorder)
                {
                    return PaymentCorePhase<SaleSettlement>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Pre-order create and balance payment cannot be combined.",
                        Errors = { "IsPreorder and PreorderBalanceOrderId cannot both be set." },
                        DiagnosticCode = "PREORDER_BALANCE_EXCLUSIVE"
                    });
                }

                var balanceGuard = await _preorderService.ValidateBalancePaymentAsync(
                    preorderBalanceId,
                    totalAmount,
                    licenseCheckCancellation).ConfigureAwait(false);
                if (!balanceGuard.Ok)
                {
                    return PaymentCorePhase<SaleSettlement>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = balanceGuard.Message ?? "Pre-order balance payment is not allowed.",
                        Errors = { balanceGuard.Message ?? "Pre-order balance payment is not allowed." },
                        DiagnosticCode = balanceGuard.Code
                    });
                }
            }

            var limitRejection = await TryRejectSaleForTenantLimitsAsync(
                    preconditions.TenantIdForFlags,
                    totalAmount,
                    offlineTransactionId,
                    licenseCheckCancellation)
                .ConfigureAwait(false);
            if (limitRejection != null)
                return PaymentCorePhase<SaleSettlement>.Fail(limitRejection);

            var methodResolution = await _paymentMethodCatalog.ResolveForPaymentAsync(
                request.Payment.Method,
                cashRegisterId);
            if (!methodResolution.Ok)
            {
                return PaymentCorePhase<SaleSettlement>.Fail(new PaymentResult
                {
                    Success = false,
                    Message = methodResolution.ErrorMessage ?? "Invalid payment method.",
                    Errors = { methodResolution.ErrorMessage ?? "Invalid payment method." }
                });
            }

            // POS sends method code "voucher". If tenant catalog maps code voucher to a non-voucher legacy value, reject before committing (no silent skip of Gutschein redemption).
            if (sale.VoucherLikelyFromClientMethod
                && methodResolution.MatchedPaymentMethodDefinition
                && !_voucherLedger.IsVoucherLegacyPayment(methodResolution.LegacyRaw))
            {
                var expectedLegacy = ((int)PaymentMethod.Voucher).ToString(CultureInfo.InvariantCulture);
                _logger.LogError(
                    "Payment rejected: payment_method_definitions has code voucher but legacy_payment_method_value={Legacy} (expected {Expected}). Fix tenant catalog.",
                    methodResolution.LegacyRaw,
                    expectedLegacy);
                return PaymentCorePhase<SaleSettlement>.Fail(new PaymentResult
                {
                    Success = false,
                    Message =
                        $"Voucher payment method is misconfigured: catalog code \"voucher\" must map to legacy value {expectedLegacy} (PaymentMethod.Voucher).",
                    Errors =
                    {
                        $"Voucher catalog misconfiguration: code voucher must use legacy_payment_method_value {expectedLegacy}."
                    },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "VOUCHER_LEGACY_MISMATCH"
                });
            }

            var isVoucherMethodResolved = _voucherLedger.IsVoucherLegacyPayment(methodResolution.LegacyRaw);
            var expectedVoucherTotal = decimal.Round(totalAmount, 2, MidpointRounding.AwayFromZero);
            // Gateway intents capture the remainder after voucher, never the cart gross.
            var expectedGatewayAmount = expectedVoucherTotal;
            if (!isVoucherMethodResolved && sale.HasVoucherPayload)
            {
                if (!request.Payment.Amount.HasValue)
                {
                    return PaymentCorePhase<SaleSettlement>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Settlement amount is required when voucher redemption is combined with a non-voucher payment method.",
                        Errors =
                        {
                            "Settlement amount is required when voucher redemption is combined with a non-voucher payment method."
                        },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "VOUCHER_MIXED_SETTLEMENT_AMOUNT_REQUIRED"
                    });
                }

                var settlementAmount = decimal.Round(request.Payment.Amount.Value, 2, MidpointRounding.AwayFromZero);
                if (settlementAmount < 0)
                {
                    return PaymentCorePhase<SaleSettlement>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Settlement amount cannot be negative.",
                        Errors = { "Settlement amount cannot be negative." },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "VOUCHER_MIXED_SETTLEMENT_NEGATIVE"
                    });
                }

                if (settlementAmount > totalAmount + 0.01m)
                {
                    return PaymentCorePhase<SaleSettlement>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Settlement amount cannot exceed total amount.",
                        Errors = { "Settlement amount cannot exceed total amount." },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "VOUCHER_MIXED_SETTLEMENT_EXCEEDS_TOTAL"
                    });
                }

                expectedVoucherTotal = decimal.Round(totalAmount - settlementAmount, 2, MidpointRounding.AwayFromZero);
                expectedGatewayAmount = settlementAmount;
                if (expectedVoucherTotal <= 0)
                {
                    return PaymentCorePhase<SaleSettlement>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Voucher redemption amount must be greater than zero in mixed voucher payments.",
                        Errors = { "Voucher redemption amount must be greater than zero in mixed voucher payments." },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "VOUCHER_MIXED_ZERO_REDEEM"
                    });
                }
            }
            else if (request.Payment.Amount.HasValue)
            {
                expectedGatewayAmount = decimal.Round(
                    request.Payment.Amount.Value,
                    2,
                    MidpointRounding.AwayFromZero);
            }

            CardPaymentTransaction? validatedCardTransaction = null;
            var gatewayIntentId = request.Payment.CardPaymentIntentId
                ?? request.Payment.OnlinePaymentId
                ?? request.Payment.GatewayIntentId;
            var requiresGatewayIntent = IsCardLegacyPayment(methodResolution.LegacyRaw)
                || OnlinePaymentMethods.Normalize(request.Payment.Method) is not null
                || gatewayIntentId.HasValue;
            if (requiresGatewayIntent && _cardPaymentService != null)
            {
                var intentToValidate = gatewayIntentId is Guid gid && gid != Guid.Empty ? gid : Guid.Empty;
                var (cardOk, cardTxn, cardErrCode, cardErrMsg) = await _cardPaymentService
                    .ValidateForFiscalPaymentAsync(intentToValidate, expectedGatewayAmount, cashRegisterId)
                    .ConfigureAwait(false);
                if (!cardOk)
                {
                    return PaymentCorePhase<SaleSettlement>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = cardErrMsg ?? "Card payment intent validation failed.",
                        Errors = { cardErrMsg ?? "Card payment intent validation failed." },
                        IsDeterministicFailure = true,
                        DiagnosticCode = cardErrCode ?? "CARD_INTENT_INVALID"
                    });
                }

                validatedCardTransaction = cardTxn;
            }

            // Voucher redemption when method is voucher, or when voucher payload is explicitly combined with a non-voucher settlement.
            List<VoucherRedeemLine>? voucherRedeemLinesForCommit = null;
            if (isVoucherMethodResolved || sale.HasVoucherPayload)
            {
                effectiveTseRequired = _tseOptions.RequiresFiscalSignature;
                var (voucherPlanError, voucherLines) = await _voucherLedger.BuildVoucherRedemptionPlanAsync(
                    totals.EffectiveTenantId,
                    cashRegisterId,
                    expectedVoucherTotal,
                    request.Payment,
                    CancellationToken.None);
                if (voucherPlanError != null)
                    return PaymentCorePhase<SaleSettlement>.Fail(voucherPlanError);

                voucherRedeemLinesForCommit = voucherLines;
            }

            var timeSyncWarningForPayment = false;
            var ntpEff = await _ntpEffectiveSettings.GetEffectiveAsync(CancellationToken.None)
                .ConfigureAwait(false);
            if (effectiveTseRequired && ntpEff.Enabled)
            {
                if (!_ntpTimeSyncStatus.ShouldAllowOnlineFiscalPayment(
                        ntpEff,
                        out var clockMsg))
                {
                    if (offlineTransactionId.HasValue)
                    {
                        timeSyncWarningForPayment = true;
                        _logger.LogWarning(
                            "Fiscal payment from offline replay accepted with NTP drift flag (TimeSyncWarning). OfflineTransactionId={OfflineId}",
                            offlineTransactionId);
                    }
                    else
                    {
                        _logger.LogWarning("Payment rejected: NTP/system time outside RKSV tolerance.");
                        return PaymentCorePhase<SaleSettlement>.Fail(new PaymentResult
                        {
                            Success = false,
                            Message = clockMsg ?? "Systemzeit nicht synchronisiert – bitte Administrator kontaktieren",
                            Errors = { clockMsg ?? "Systemzeit nicht synchronisiert – bitte Administrator kontaktieren" },
                            IsDeterministicFailure = true,
                            DiagnosticCode = "NTP_TIME_SYNC"
                        });
                    }
                }
            }

            return PaymentCorePhase<SaleSettlement>.Ok(new SaleSettlement
            {
                MethodResolution = methodResolution,
                ValidatedCardTransaction = validatedCardTransaction,
                VoucherRedeemLines = voucherRedeemLinesForCommit,
                TimeSyncWarning = timeSyncWarningForPayment,
                EffectiveTseRequired = effectiveTseRequired,
            });
        }

        /// <summary>
        /// Allocates the Beleg number and stages payment, signature, invoice, receipt, and voucher rows.
        /// Does not call SaveChanges, Commit, or BeginTransaction.
        /// </summary>
        private async Task<PaymentCorePhase<PersistedFiscalDocuments>> PersistFiscalDocumentsAsync(
            CreatePaymentRequest request,
            string userId,
            IDbContextTransaction transaction,
            PaymentPreconditions preconditions,
            PreparedFiscalSale sale,
            SaleTotals totals,
            SaleSettlement settlement,
            Guid cashRegisterId,
            string registerNumber,
            string? idempotencyKey,
            Guid? offlineTransactionId,
            Guid? offlineReplayBatchCorrelationId)
        {
            var totalAmount = totals.TotalAmount;
            var totalTaxAmount = totals.TotalTaxAmount;
            var taxDetails = totals.TaxDetails;
            var taxResult = totals.TaxResult;
            var paymentItems = totals.PaymentItems;
            var effectiveTseRequired = settlement.EffectiveTseRequired;

            // Single database transaction: register row lock + stock + BelegNr + payment + invoice + receipt commit together.
            // Sprint 1: Allocate BelegNr within this transaction so it commits or rolls back with payment/invoice/receipt/stock.
            var preReceiptNumber = !string.IsNullOrWhiteSpace(request.ReservedReceiptNumber)
                ? request.ReservedReceiptNumber.Trim()
                : await AllocateCountryBelegNrAsync(sale.CountryBinding, transaction, cashRegisterId, registerNumber);

            if (!string.IsNullOrWhiteSpace(request.ReservedReceiptNumber))
            {
                var reservedTaken = await _context.PaymentDetails
                    .AnyAsync(p => p.ReceiptNumber == preReceiptNumber && p.IsActive);
                if (reservedTaken)
                {
                    return PaymentCorePhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Reserved receipt number is already in use.",
                        Errors = { "Reserved receipt number conflict." },
                        IsDeterministicFailure = false,
                        DiagnosticCode = "RECEIPT_NUMBER_CONFLICT"
                    });
                }
            }

            // Cash tender is operational only. It is not part of the RKSV signature or the TSE payload.
            decimal? cashGiven = null;
            decimal? changeDue = null;
            var isCashSale = !request.IsStorno
                && !request.IsRefund
                && string.Equals(
                    settlement.MethodResolution.LegacyRaw,
                    ((int)PaymentMethod.Cash).ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal);
            if (isCashSale && request.CashGiven is decimal tendered)
            {
                cashGiven = decimal.Round(tendered, 2, MidpointRounding.AwayFromZero);
                var due = decimal.Round(cashGiven.Value - totalAmount, 2, MidpointRounding.AwayFromZero);
                changeDue = due < 0m ? 0m : due;
            }

            // Ödeme detayları oluştur (totals = CartMoneyHelper toplamları; optional customer % discount already applied above)
            var payment = new PaymentDetails
            {
                CustomerId = sale.Customer.Id,
                CustomerName = sale.Customer.Name,
                PaymentItems = JsonDocument.Parse(JsonSerializer.Serialize(paymentItems)),
                TotalAmount = totalAmount,
                TaxAmount = totalTaxAmount,
                CashGiven = cashGiven,
                ChangeDue = changeDue,
                TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(taxDetails)),
                PaymentMethodRaw = settlement.MethodResolution.LegacyRaw,
                Notes = request.Notes,
                PrescriptionReference = preconditions.PrescriptionReference,
                RouteFrom = preconditions.RouteFrom,
                RouteTo = preconditions.RouteTo,
                RouteKm = preconditions.RouteKm,
                TripStartedAtUtc = preconditions.TripStartedAtUtc,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                TableNumber = request.TableNumber,
                CashierId = userId,
                Steuernummer = sale.EffectiveSteuernummer,
                CompanyName = sale.CompanyProfile.CompanyName,
                CompanyAddress = CompanyProfileMapper.FormatCompanyAddress(
                    sale.CompanyProfile.Street, sale.CompanyProfile.ZipCode, sale.CompanyProfile.City),
                CashRegisterId = cashRegisterId,
                TseTimestamp = DateTime.UtcNow,
                IsPrinted = false,
                ReceiptNumber = preReceiptNumber,
                AppliedBenefitsSnapshot = totals.AppliedBenefitsSnapshot,
                IdempotencyKey = idempotencyKey,
                OfflineTransactionId = offlineTransactionId,
                OfflineReplayBatchCorrelationId = offlineReplayBatchCorrelationId,
                TimeSyncWarning = settlement.TimeSyncWarning,
                TransactionId = settlement.ValidatedCardTransaction?.GatewayTransactionId,
                Provider = settlement.ValidatedCardTransaction?.Gateway
            };
            FiscalDocumentCountryStamp.Apply(payment, sale.CountryBinding);

            if (sale.CountryBinding.Profile.FiscalSystem == FiscalSystem.MWST_CH)
            {
                try
                {
                    var chSign = await _fiscalSignatureRouter.SignAsync(
                        new FiscalSignatureContext(
                            sale.CountryBinding,
                            taxResult.Lines.Select(line => TaxLineItemInput.FromVatPercent(
                                line.UnitPriceGross,
                                1,
                                line.TaxRate * 100m)).ToList(),
                            payment.TotalAmount,
                            preReceiptNumber));
                    payment.SwissQrText = chSign.SwissQrText;
                    _logger.LogInformation(
                        "CH_MWST QR built. TenantId={TenantId} Receipt={Receipt} Provider={Provider} Vat={Vat}",
                        sale.CountryBinding.Settings.TenantId,
                        preReceiptNumber,
                        chSign.Provider,
                        chSign.TotalVat);
                    try
                    {
                        await _auditLogService.LogSystemOperationAsync(
                            action: "CH_MWST_QR_BUILT",
                            entityType: "Payment",
                            userId: userId,
                            userRole: "Cashier",
                            description: "CH MWST QR payload built for canary tenant",
                            actionType: AuditEventType.ChMwstQrBuilt,
                            entityId: payment.Id,
                            tenantId: sale.CountryBinding.Settings.TenantId);
                    }
                    catch (Exception auditEx)
                    {
                        _logger.LogWarning(auditEx, "CH_MWST audit write failed for {Receipt}", preReceiptNumber);
                    }
                }
                catch (FeatureDisabledException ex)
                {
                    return PaymentCorePhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Swiss MWST is not enabled",
                        Errors = { ex.Message },
                        DiagnosticCode = "CH_FLAG_OFF"
                    });
                }
                catch (ChMwstCanaryRejectedException ex)
                {
                    return PaymentCorePhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Swiss MWST canary rejected the tenant",
                        Errors = { ex.Message },
                        DiagnosticCode = "CH_NOT_CANARY"
                    });
                }
            }
            else if (sale.CountryBinding.Profile.Code == CountryProfileCodes.EuDefault)
            {
                try
                {
                    var euSign = await _fiscalSignatureRouter.SignAsync(
                        new FiscalSignatureContext(
                            sale.CountryBinding,
                            taxResult.Lines.Select(line => TaxLineItemInput.FromVatPercent(
                                line.UnitPriceGross,
                                1,
                                line.TaxRate * 100m)).ToList(),
                            payment.TotalAmount,
                            preReceiptNumber,
                            BuyerVatId: request.Steuernummer));
                    _logger.LogInformation(
                        "EN_16931 Peppol path. TenantId={TenantId} Receipt={Receipt} Status={Status}",
                        sale.CountryBinding.Settings.TenantId,
                        preReceiptNumber,
                        euSign.PeppolStatus);
                }
                catch (FeatureDisabledException ex)
                {
                    return PaymentCorePhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "EN 16931 is not enabled",
                        Errors = { ex.Message },
                        DiagnosticCode = "EU_FLAG_OFF"
                    });
                }
            }
            else if (sale.CountryBinding.Profile.FiscalSystem == FiscalSystem.RKSV_AT && effectiveTseRequired)
            {
                var requiresPrescriptionByProduct = new Dictionary<Guid, bool>();
                foreach (var priced in totals.PricedLines)
                    requiresPrescriptionByProduct[priced.Product.Id] = priced.Product.RequiresPrescription;

                (taxResult, taxDetails) = await ApplyBeforeSignCartAsync(
                    totals.EffectiveTenantId,
                    payment,
                    paymentItems,
                    taxResult,
                    taxDetails,
                    sale.TaxStrategy,
                    sale.CountryBinding,
                    totals.RateCatalog,
                    request.Steuernummer,
                    requiresPrescriptionByProduct);
                totalAmount = payment.TotalAmount;
                totalTaxAmount = payment.TaxAmount;
                try
                {
                    var atSign = await _fiscalSignatureRouter.SignAsync(
                        new FiscalSignatureContext(
                            sale.CountryBinding,
                            taxResult.Lines.Select(line => TaxLineItemInput.FromVatPercent(
                                line.UnitPriceGross,
                                1,
                                line.TaxRate * 100m)).ToList(),
                            payment.TotalAmount,
                            preReceiptNumber,
                            CashRegisterId: cashRegisterId,
                            RegisterNumber: registerNumber,
                            TaxDetailsJson: JsonSerializer.Serialize(taxDetails),
                            DbTransaction: transaction));
                    payment.TseSignature = atSign.Signature!;
                    payment.PrevSignatureValueUsed = atSign.PrevSignatureValue;
                    payment.CertificateThumbprint = atSign.CertificateThumbprint;
                    _logger.LogInformation(
                        "TSE signature generated for payment {PaymentId} provider={Provider}",
                        payment.Id,
                        atSign.Provider);
                }
                catch (TseUnavailableException ex)
                {
                    _logger.LogError(
                        ex,
                        "TSE unavailable while signing payment (BelegNr={BelegNr}, paymentDraftId={PaymentId}).",
                        preReceiptNumber,
                        payment.Id);
                    return PaymentCorePhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "TSE is not available",
                        Errors = { ex.Message },
                        DiagnosticCode = "TSE_UNAVAILABLE"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to generate TSE signature (BelegNr={BelegNr}, effectiveTseRequired={Effective}, clientTseRequested={ClientTse}, paymentDraftId={PaymentId}). InnerException surface in logs from TseService phase.",
                        preReceiptNumber,
                        effectiveTseRequired,
                        request.Payment.TseRequired,
                        payment.Id);
                    return PaymentCorePhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Failed to generate TSE signature",
                        Errors = { "TSE signature generation failed" }
                    });
                }
            }
            else if (sale.CountryBinding.Profile.FiscalSystem == FiscalSystem.KASSENSICHERHEIT_DE)
            {
                var deSign = await _fiscalSignatureRouter.SignAsync(
                    new FiscalSignatureContext(
                        sale.CountryBinding,
                        taxResult.Lines.Select(line => TaxLineItemInput.FromVatPercent(
                            line.UnitPriceGross,
                            1,
                            line.TaxRate * 100m)).ToList(),
                        payment.TotalAmount,
                        preReceiptNumber,
                        CashRegisterId: cashRegisterId,
                        RegisterNumber: registerNumber,
                        TaxDetailsJson: JsonSerializer.Serialize(taxDetails),
                        PaymentMethodRaw: payment.PaymentMethodRaw,
                        PaymentId: payment.Id,
                        DbTransaction: transaction));
                payment.CountryCodeAtIssue = "DE";
            }

            var companyAddress = payment.CompanyAddress
                ?? CompanyProfileMapper.FormatCompanyAddress(
                    sale.CompanyProfile.Street, sale.CompanyProfile.ZipCode, sale.CompanyProfile.City);

            var posInvoice = new Invoice
            {
                Id = Guid.NewGuid(),
                SourcePaymentId = payment.Id,
                InvoiceNumber = payment.ReceiptNumber,
                InvoiceDate = payment.CreatedAt,
                DueDate = payment.CreatedAt,
                Status = InvoiceStatus.Paid,
                Subtotal = payment.TotalAmount - payment.TaxAmount,
                TaxAmount = payment.TaxAmount,
                TotalAmount = payment.TotalAmount,
                PaidAmount = payment.TotalAmount,
                RemainingAmount = 0,
                CustomerName = payment.CustomerName,
                CustomerTaxNumber = payment.Steuernummer,
                CompanyName = payment.CompanyName ?? sale.CompanyProfile.CompanyName,
                CompanyTaxNumber = payment.Steuernummer,
                CompanyAddress = companyAddress,
                TseSignature = payment.TseSignature ?? string.Empty,
                KassenId = registerNumber,
                TseTimestamp = payment.TseTimestamp,
                CashRegisterId = cashRegisterId,
                PaymentMethod = payment.PaymentMethod,
                PaymentReference = payment.TransactionId,
                PaymentDate = payment.CreatedAt,
                InvoiceItems = payment.PaymentItems,
                TaxDetails = payment.TaxDetails,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            FiscalDocumentCountryStamp.CopyFromPayment(posInvoice, payment, sale.CountryBinding);

            _context.PaymentDetails.Add(payment);
            IReadOnlyList<IssuedTicketDto> issuedTickets = [];
            if (_ticketRedemptions is not null
                && await TenantIssuesTicketsAsync(CancellationToken.None).ConfigureAwait(false)
                && !request.IsStorno
                && !request.IsRefund)
            {
                var ticketLines = totals.PricedLines
                    .Where(line => line.Product.IsTicket)
                    .Select(line => (line.Product, line.Item.Quantity))
                    .ToList();
                if (ticketLines.Count > 0)
                {
                    issuedTickets = await _ticketRedemptions
                        .IssueForSaleAsync(
                            totals.EffectiveTenantId,
                            payment.Id,
                            ticketLines,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }

            foreach (var imeiRow in totals.PendingImeiRows)
            {
                imeiRow.Status = ProductImeiStatus.Sold;
                imeiRow.SoldPaymentId = payment.Id;
                imeiRow.SoldAtUtc = DateTime.UtcNow;
            }

            _context.Invoices.Add(posInvoice);
            await _receiptService.AddReceiptFromPaymentToContextAsync(payment);

            if (settlement.VoucherRedeemLines is { Count: > 0 })
            {
                var receiptEntity = _context.ChangeTracker.Entries<Receipt>()
                    .Select(e => e.Entity)
                    .FirstOrDefault(r => r.PaymentId == payment.Id);
                if (receiptEntity == null)
                {
                    return PaymentCorePhase<PersistedFiscalDocuments>.Fail(new PaymentResult
                    {
                        Success = false,
                        Message = "Internal error: receipt not attached for voucher redemption.",
                        Errors = { "Receipt creation failed before voucher ledger." },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "VOUCHER_RECEIPT_MISSING"
                    });
                }

                var voucherApplyError = await _voucherLedger.ApplyVoucherRedemptionsInCurrentTransactionAsync(
                    totals.EffectiveTenantId,
                    userId,
                    payment,
                    receiptEntity.ReceiptId,
                    settlement.VoucherRedeemLines,
                    idempotencyKey,
                    CancellationToken.None);
                if (voucherApplyError != null)
                    return PaymentCorePhase<PersistedFiscalDocuments>.Fail(voucherApplyError);
            }

            // Stock updates: when EnforceStockOnSales is true, product entities were modified in the loop (tracked); SaveChanges persists them with payment/invoice/receipt in one commit.
            return PaymentCorePhase<PersistedFiscalDocuments>.Ok(new PersistedFiscalDocuments
            {
                Payment = payment,
                Invoice = posInvoice,
                IssuedTickets = issuedTickets,
                EffectiveTenantId = totals.EffectiveTenantId,
            });
        }

        /// <summary>
        /// Audits, FinanzOnline dispatch, card-intent link, and the success result after the fiscal commit.
        /// </summary>
        private async Task<PaymentResult> DispatchPostCommitAsync(
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

            var (qrPayload, isDemoFiscal, tseProvider) = await BuildQrPayloadAndFlagsAsync(createdPayment, effectiveTseRequired);
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

        /// <summary>
        /// Maps a fiscal-commit exception after the caller has rolled the transaction back and cleared the tracker.
        /// </summary>
        private async Task<FiscalCommitFailure> MapFiscalCommitFailureAsync(
            Exception ex,
            string? idempotencyKey,
            Guid customerId)
        {
            if (ex is DbUpdateConcurrencyException concurrency)
            {
                _logger.LogWarning(concurrency, "Daily allowance concurrency conflict for customer {CustomerId} during payment creation", customerId);
                return FiscalCommitFailure.Return(ToDailyAllowanceConflictResult());
            }

            if (ex is DbUpdateException idempotencyEx && IsIdempotencyKeyViolation(idempotencyEx))
            {
                if (idempotencyKey != null)
                {
                    var existing = await _context.PaymentDetails.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);
                    if (existing != null)
                    {
                        var invoiceExists = await _context.Invoices.AsNoTracking().AnyAsync(i => i.SourcePaymentId == existing.Id);
                        _logger.LogInformation("Idempotency key race: returning existing payment {PaymentId} for key {Key}", existing.Id, idempotencyKey);
                        var (qr, demo, provider) = await BuildQrPayloadAndFlagsAsync(existing, !string.IsNullOrEmpty(existing.TseSignature));
                        return FiscalCommitFailure.Return(new PaymentResult
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
                        });
                    }
                }

                if (IsVoucherLedgerIdempotencyConstraintViolation(idempotencyEx))
                {
                    const string msg = "Voucher already used in this transaction. Please retry with a new idempotency key.";
                    _logger.LogWarning(idempotencyEx, "Voucher ledger idempotency unique constraint violated during payment create");
                    return FiscalCommitFailure.Return(new PaymentResult
                    {
                        Success = false,
                        Message = msg,
                        Errors = { msg },
                        IsDeterministicFailure = true,
                        DiagnosticCode = "VOUCHER_LEDGER_IDEMPOTENCY_CONFLICT"
                    });
                }

                if (TryGetPostgresException(idempotencyEx, out var pgExIdem))
                {
                    _logger.LogError(
                        pgExIdem,
                        "PostgreSQL violation (idempotency catch path): SqlState={SqlState}, Constraint={Constraint}, Detail={Detail}",
                        pgExIdem.SqlState,
                        pgExIdem.ConstraintName,
                        pgExIdem.Detail);
                    if (TryMapPaymentCommitPostgresException(pgExIdem, out var pgMappedIdem))
                        return FiscalCommitFailure.Return(pgMappedIdem);
                }

                _logger.LogError(idempotencyEx, "Fiscal transaction failed (idempotency key violation) for payment");
                return FiscalCommitFailure.Throw();
            }

            if (ex is DbUpdateException benefitEx && IsBenefitDailyUsageConflict(benefitEx))
            {
                _logger.LogWarning(benefitEx, "Daily allowance unique constraint race for customer {CustomerId} during payment creation", customerId);
                return FiscalCommitFailure.Return(ToDailyAllowanceConflictResult());
            }

            if (ex is DbUpdateException saveEx)
            {
                if (TryGetPostgresException(saveEx, out var pgExSave))
                {
                    _logger.LogError(
                        pgExSave,
                        "PostgreSQL violation (payment commit): SqlState={SqlState}, Constraint={Constraint}, Detail={Detail}",
                        pgExSave.SqlState,
                        pgExSave.ConstraintName,
                        pgExSave.Detail);
                    if (TryMapPaymentCommitPostgresException(pgExSave, out var pgMappedSave))
                        return FiscalCommitFailure.Return(pgMappedSave);
                }

                _logger.LogError(saveEx, "Fiscal transaction failed for payment; DbUpdateException after rollback");
                return FiscalCommitFailure.Throw();
            }

            if (ex is ExtensionFailedException extensionEx)
            {
                _logger.LogWarning(
                    extensionEx,
                    "Tenant extension {Feature} rejected payment before commit",
                    extensionEx.FeatureName);
                return FiscalCommitFailure.Return(ExtensionRejected(extensionEx));
            }

            _logger.LogError(ex, "Fiscal transaction failed for payment; Payment, Invoice, Receipt, and stock updates rolled back");
            return FiscalCommitFailure.Throw();
        }

        private sealed class PaymentCorePhase<T>
        {
            public PaymentResult? Stop { get; init; }
            public T? Value { get; init; }
            public bool TransactionAlreadyRolledBack { get; init; }

            public static PaymentCorePhase<T> Ok(T value) => new() { Value = value };

            public static PaymentCorePhase<T> Fail(PaymentResult error, bool alreadyRolledBack = false) =>
                new() { Stop = error, TransactionAlreadyRolledBack = alreadyRolledBack };
        }

        private sealed class PaymentPreconditions
        {
            public Guid TenantIdForFlags { get; init; }
            public string? PrescriptionReference { get; init; }
            public string? RouteFrom { get; init; }
            public string? RouteTo { get; init; }
            public decimal? RouteKm { get; init; }
            public DateTime? TripStartedAtUtc { get; init; }
        }

        private sealed class PreparedFiscalSale
        {
            public required Customer Customer { get; init; }
            public required CompanyProfileOptions CompanyProfile { get; init; }
            public required CountryStrategyBinding CountryBinding { get; init; }
            public required ITaxStrategy TaxStrategy { get; init; }
            public required string EffectiveSteuernummer { get; init; }
            public required System.Security.Claims.ClaimsPrincipal Principal { get; init; }
            public required bool VoucherLikelyFromClientMethod { get; init; }
            public required bool HasVoucherPayload { get; init; }
            public required bool EffectiveTseRequired { get; init; }
        }

        private sealed class CommitRegister
        {
            public required Guid CashRegisterId { get; init; }
            public required string RegisterNumber { get; init; }
        }

        private sealed class SaleTotals
        {
            public required Guid EffectiveTenantId { get; init; }
            public required List<PaymentItem> PaymentItems { get; init; }
            public required decimal TotalAmount { get; init; }
            public required decimal TotalTaxAmount { get; init; }
            public required Dictionary<string, decimal> TaxDetails { get; init; }
            public JsonDocument? AppliedBenefitsSnapshot { get; init; }
            public required TaxCalculationResult TaxResult { get; init; }
            public required List<(PaymentItemRequest Item, Product Product, decimal UnitGross)> PricedLines { get; init; }
            public required List<ProductImei> PendingImeiRows { get; init; }
            public required int PaymentStockLinesSkipped { get; init; }
            public required ICountryTaxTypeRegistry RateCatalog { get; init; }
        }

        private sealed class SaleSettlement
        {
            public required PaymentMethodResolutionResult MethodResolution { get; init; }
            public CardPaymentTransaction? ValidatedCardTransaction { get; init; }
            public List<VoucherRedeemLine>? VoucherRedeemLines { get; init; }
            public required bool TimeSyncWarning { get; init; }
            public required bool EffectiveTseRequired { get; init; }
        }

        private sealed class PersistedFiscalDocuments
        {
            public required PaymentDetails Payment { get; init; }
            public required Invoice Invoice { get; init; }
            public required IReadOnlyList<IssuedTicketDto> IssuedTickets { get; init; }
            public required Guid EffectiveTenantId { get; init; }
        }

        private sealed class FiscalCommitFailure
        {
            public PaymentResult? Result { get; init; }
            public bool Rethrow { get; init; }

            public static FiscalCommitFailure Return(PaymentResult result) => new() { Result = result };

            public static FiscalCommitFailure Throw() => new() { Rethrow = true };
        }
    }
}
