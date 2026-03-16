using Microsoft.EntityFrameworkCore;
using Npgsql;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Data.Repositories;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Ödeme işlemleri için service implementation
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IGenericRepository<PaymentDetails> _paymentRepository;
        private readonly IGenericRepository<Product> _productRepository;
        private readonly IGenericRepository<Customer> _customerRepository;
        private readonly ITseService _tseService;
        private readonly IFinanzOnlineService _finanzOnlineService;
        private readonly ILogger<PaymentService> _logger;
        private readonly IUserService _userService;
        private readonly CompanyProfileOptions _companyProfile;
        private readonly TseOptions _tseOptions;
        private readonly IProductModifierValidationService _modifierValidation;

        public PaymentService(
            AppDbContext context,
            IGenericRepository<PaymentDetails> paymentRepository,
            IGenericRepository<Product> productRepository,
            IGenericRepository<Customer> customerRepository,
            ITseService tseService,
            IFinanzOnlineService finanzOnlineService,
            IUserService userService,
            IProductModifierValidationService modifierValidation,
            Microsoft.Extensions.Options.IOptions<CompanyProfileOptions> companyProfile,
            Microsoft.Extensions.Options.IOptions<TseOptions> tseOptions,
            ILogger<PaymentService> logger)
        {
            _context = context;
            _paymentRepository = paymentRepository;
            _productRepository = productRepository;
            _customerRepository = customerRepository;
            _tseService = tseService;
            _finanzOnlineService = finanzOnlineService;
            _userService = userService;
            _modifierValidation = modifierValidation;
            _companyProfile = companyProfile.Value;
            _tseOptions = tseOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// Yeni ödeme oluştur
        /// </summary>
        public async Task<PaymentResult> CreatePaymentAsync(CreatePaymentRequest request, string userId)
        {
            try
            {
                _logger.LogInformation("Creating payment for customer {CustomerId} by user {UserId}", request.CustomerId, userId);

                // Identity first: never trust body cashierId for who pays. Mismatch → 403 CASHIER_ID_MISMATCH only
                // (avoids conflating payload drift with demo rejection on the authenticated user).
                var payloadCashierId = (request.CashierId ?? string.Empty).Trim();
                var placeholderCashierIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "UNKNOWN", "current-user", ""
                };
                if (!placeholderCashierIds.Contains(payloadCashierId) &&
                    !string.Equals(payloadCashierId, userId, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "Payment rejected: CashierId mismatch. AuthenticatedUserId={AuthenticatedUserId} PayloadCashierId={PayloadCashierId} RejectionCode={RejectionCode}",
                        userId,
                        request.CashierId ?? "",
                        "CASHIER_ID_MISMATCH");
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "CashierId must match the authenticated user",
                        Errors = { "Payment identity mismatch: cashierId must equal the signed-in user id." },
                        DiagnosticCode = "CASHIER_ID_MISMATCH"
                    };
                }

                // Demo gate uses authenticated user only (resolved by userId — same as JWT). Single resolved user.
                var user = await _userService.GetUserByIdAsync(userId);
                if (DemoUserHelper.IsDemoUser(user))
                {
                    var rejectionReason = DemoUserHelper.GetDemoRejectionReason(user) ?? "DEMO_UNKNOWN";
                    _logger.LogWarning(
                        "Payment demo rejection: AuthenticatedUserId={AuthenticatedUserId} AuthenticatedUserEmail={AuthenticatedUserEmail} PayloadCashierId={PayloadCashierId} ResolvedUserId={ResolvedUserId} ResolvedUserEmail={ResolvedUserEmail} ResolvedUserRole={ResolvedUserRole} ResolvedUserIsDemo={ResolvedUserIsDemo} RejectionCode={RejectionCode}",
                        userId,
                        user?.Email ?? "",
                        request.CashierId ?? "",
                        user?.Id ?? "",
                        user?.Email ?? "",
                        user?.Role ?? "",
                        user?.IsDemo ?? false,
                        rejectionReason);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Demo users cannot create real payments",
                        Errors = { "Demo users are restricted to test operations only" },
                        DiagnosticCode = rejectionReason
                    };
                }

                // Müşteri kontrolü
                var customer = await _customerRepository.GetByIdAsync(request.CustomerId);
                if (customer == null)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Customer not found",
                        Errors = { "Customer not found" }
                    };
                }

                // Idempotency: if client sent a key and we already have a payment for it, return that result (no duplicate creation)
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                {
                    var existing = await _context.PaymentDetails
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdempotencyKey == request.IdempotencyKey);
                    if (existing != null)
                    {
                        _logger.LogInformation("Idempotent payment request: returning existing payment {PaymentId} for key {Key}", existing.Id, request.IdempotencyKey);
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
                            TseProvider = provider
                        };
                    }
                }

                // Steuernummer (request) kontrolü - ATU formatı
                if (!IsValidAustrianTaxNumber(request.Steuernummer))
                {
                    _logger.LogWarning("Invalid Austrian tax number in request: {TaxNumber}", request.Steuernummer);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Invalid Austrian tax number format",
                        Errors = { "Steuernummer must be in ATU format (e.g., ATU12345678)" }
                    };
                }

                // TSE modu: Off = tseRequired yok sayılır, Demo = cihaz atlanır, Device = cihaz zorunlu
                var effectiveTseRequired = request.Payment.TseRequired && !_tseOptions.IsOff;
                if (effectiveTseRequired && !_tseOptions.UseSoftTseWhenNoDevice)
                {
                    var tseStatus = await _tseService.GetDeviceStatusAsync();
                    if (!tseStatus.IsConnected)
                    {
                        _logger.LogError("TSE device not connected. Cannot create payment requiring TSE signature");
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "TSE device not connected",
                            Errors = { "TSE device must be connected for this payment type" }
                        };
                    }
                    if (!tseStatus.IsReady)
                    {
                        _logger.LogError("TSE device not ready. Status: {Status}", tseStatus.Status);
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "TSE device not ready",
                            Errors = { $"TSE device is not ready. Status: {tseStatus.Status}" }
                        };
                    }
                }

                // Ürün kontrolü ve stok güncelleme. Tek hesap motoru: CartMoneyHelper (gross model).
                var paymentItems = new List<PaymentItem>();
                var productIdToCategoryId = new Dictionary<Guid, Guid>();
                decimal totalAmount = 0;
                decimal totalTaxAmount = 0;
                var taxDetails = new Dictionary<string, decimal>();

                foreach (var itemRequest in request.Items)
                {
                    var product = await _context.Products
                        .Include(p => p.CategoryNavigation)
                        .FirstOrDefaultAsync(p => p.Id == itemRequest.ProductId);
                    if (product == null)
                    {
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "Product not found",
                            Errors = { $"Product with ID {itemRequest.ProductId} not found" }
                        };
                    }

                    if (product.CategoryNavigation == null)
                    {
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "Product category missing",
                            Errors = { $"Product {product.Name} has no category" }
                        };
                    }

                    // Phase 2: Sellable add-ons are product-only payment lines; no stock deduction (stok düşülmez).
                    if (!product.IsSellableAddOn)
                    {
                        if (product.StockQuantity < itemRequest.Quantity)
                        {
                            return new PaymentResult
                            {
                                Success = false,
                                Message = "Insufficient stock",
                                Errors = { $"Insufficient stock for product {product.Name}" }
                            };
                        }
                        product.StockQuantity -= itemRequest.Quantity;
                        product.UpdatedAt = DateTime.UtcNow;
                        await _productRepository.UpdateAsync(product);
                    }

                    // VAT oranı kategoriden (yüzde: 10, 20); tek rounding noktası CartMoneyHelper. decimal only.
                    var vatRatePercent = product.CategoryNavigation.VatRate;
                    var line = CartMoneyHelper.ComputeLine(product.Price, itemRequest.Quantity, vatRatePercent);
                    totalAmount += line.LineGross;
                    totalTaxAmount += line.LineTax;

                    var paymentItem = new PaymentItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = itemRequest.Quantity,
                        UnitPrice = line.UnitPriceGross,
                        TotalPrice = line.LineGross,
                        TaxType = line.TaxType,
                        TaxRate = line.TaxRate,
                        TaxAmount = line.LineTax,
                        LineNet = line.LineNet
                    };

                    // Add-ons are separate payment items (productId). ModifierIds/Modifiers in request are not used for new writes.
                    paymentItems.Add(paymentItem);
                    productIdToCategoryId[product.Id] = product.CategoryId;

                    var taxKey = line.TaxType.ToString().ToLowerInvariant();
                    if (!taxDetails.ContainsKey(taxKey))
                        taxDetails[taxKey] = 0;
                    taxDetails[taxKey] += line.LineTax;
                }

                // Resolve effective percentage discount: assigned benefit (Priority then highest %) first, else Customer.DiscountPercentage fallback. Single discount only.
                decimal effectivePct = 0;
                var now = DateTime.UtcNow;
                var assignedPercentage = await _context.BenefitAssignments
                    .Where(ba => ba.CustomerId == customer.Id && ba.IsActive
                        && ba.ValidFrom <= now && (ba.ValidTo == null || ba.ValidTo >= now))
                    .Include(ba => ba.BenefitDefinition)
                    .Where(ba => ba.BenefitDefinition.BenefitKind == AppliedBenefitKind.PercentageDiscount
                        && ba.BenefitDefinition.IsActive
                        && ba.BenefitDefinition.PercentageValue.HasValue
                        && ba.BenefitDefinition.PercentageValue > 0)
                    .OrderByDescending(ba => ba.Priority)
                    .ThenByDescending(ba => ba.BenefitDefinition.PercentageValue)
                    .Select(ba => ba.BenefitDefinition.PercentageValue!.Value)
                    .FirstOrDefaultAsync();
                if (assignedPercentage > 0)
                    effectivePct = assignedPercentage;
                else
                    effectivePct = customer.DiscountPercentage;

                JsonDocument? appliedBenefitsSnapshot = null;
                if (totalAmount > 0 && effectivePct > 0)
                {
                    effectivePct = Math.Clamp(effectivePct, 0, 100);
                    var discountAmount = CartMoneyHelper.Round(totalAmount * effectivePct / 100m);
                    if (discountAmount > 0)
                    {
                        var totalBeforeDiscount = totalAmount;
                        totalAmount -= discountAmount;
                        var ratio = totalBeforeDiscount > 0 ? totalAmount / totalBeforeDiscount : 1m;
                        totalTaxAmount = CartMoneyHelper.Round(totalTaxAmount * ratio);
                        var keys = taxDetails.Keys.ToList();
                        foreach (var key in keys)
                            taxDetails[key] = CartMoneyHelper.Round(taxDetails[key] * ratio);
                        var snapshotItem = new AppliedBenefitSnapshotItem
                        {
                            Kind = AppliedBenefitKind.PercentageDiscount,
                            Description = $"Customer discount {effectivePct}%",
                            Amount = -discountAmount,
                            Quantity = null
                        };
                        appliedBenefitsSnapshot = JsonDocument.Parse(JsonSerializer.Serialize(new[] { snapshotItem }));
                    }
                }

                // Daily free allowance: resolve assigned FreeAllowance benefits (per_day, AllowanceCategoryId set), apply within daily limit, merge into snapshot.
                var todayUtc = now.Date;
                var freeAllowanceAssignments = await _context.BenefitAssignments
                    .Where(ba => ba.CustomerId == customer.Id && ba.IsActive
                        && ba.ValidFrom <= now && (ba.ValidTo == null || ba.ValidTo >= now))
                    .Include(ba => ba.BenefitDefinition)
                    .Where(ba => ba.BenefitDefinition.BenefitKind == AppliedBenefitKind.FreeAllowance
                        && ba.BenefitDefinition.IsActive
                        && ba.BenefitDefinition.AllowanceQuantity.HasValue
                        && ba.BenefitDefinition.AllowanceQuantity > 0
                        && ba.BenefitDefinition.AllowanceCategoryId.HasValue
                        && (ba.BenefitDefinition.AllowanceScope == null || string.Equals(ba.BenefitDefinition.AllowanceScope, "per_day", StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(ba => ba.Priority)
                    .ToListAsync();

                var snapshotList = appliedBenefitsSnapshot != null
                    ? JsonSerializer.Deserialize<List<AppliedBenefitSnapshotItem>>(appliedBenefitsSnapshot.RootElement.GetRawText()) ?? new List<AppliedBenefitSnapshotItem>()
                    : new List<AppliedBenefitSnapshotItem>();

                foreach (var assignment in freeAllowanceAssignments)
                {
                    var def = assignment.BenefitDefinition;
                    var allowanceQty = def.AllowanceQuantity!.Value;
                    var categoryId = def.AllowanceCategoryId!.Value;

                    var usageRow = await _context.BenefitDailyUsages
                        .FirstOrDefaultAsync(u => u.CustomerId == customer.Id && u.BenefitDefinitionId == def.Id && u.UsageDate == todayUtc);
                    if (usageRow == null)
                    {
                        usageRow = new BenefitDailyUsage
                        {
                            CustomerId = customer.Id,
                            BenefitDefinitionId = def.Id,
                            UsageDate = todayUtc,
                            QuantityUsed = 0,
                            Version = 0
                        };
                        _context.BenefitDailyUsages.Add(usageRow);
                    }

                    var remaining = allowanceQty - usageRow.QuantityUsed;
                    if (remaining <= 0) continue;

                    var eligibleItems = paymentItems
                        .Where(pi => productIdToCategoryId.GetValueOrDefault(pi.ProductId) == categoryId)
                        .ToList();
                    var eligibleQuantity = eligibleItems.Sum(i => i.Quantity);
                    var claimed = Math.Min(remaining, eligibleQuantity);
                    if (claimed <= 0) continue;

                    var remainingToClaim = claimed;
                    decimal discountAmount = 0;
                    foreach (var item in eligibleItems)
                    {
                        if (remainingToClaim <= 0) break;
                        var n = Math.Min(item.Quantity, remainingToClaim);
                        discountAmount += item.UnitPrice * n;
                        remainingToClaim -= n;
                    }
                    discountAmount = CartMoneyHelper.Round(discountAmount);
                    if (discountAmount <= 0) continue;

                    var totalBeforeAllowance = totalAmount;
                    totalAmount -= discountAmount;
                    var ratio = totalBeforeAllowance > 0 ? totalAmount / totalBeforeAllowance : 1m;
                    totalTaxAmount = CartMoneyHelper.Round(totalTaxAmount * ratio);
                    var keys = taxDetails.Keys.ToList();
                    foreach (var key in keys)
                        taxDetails[key] = CartMoneyHelper.Round(taxDetails[key] * ratio);

                    snapshotList.Add(new AppliedBenefitSnapshotItem
                    {
                        Kind = AppliedBenefitKind.FreeAllowance,
                        Description = $"Free allowance ({claimed} items)",
                        Amount = -discountAmount,
                        Quantity = claimed
                    });

                    usageRow.QuantityUsed += claimed;
                    usageRow.Version++;
                }

                // BuyXGetY: single highest-priority assignment; for every BuyXQuantity items, grant GetYQuantity free (discount cheapest units first).
                var buyXGetYAssignment = await _context.BenefitAssignments
                    .Where(ba => ba.CustomerId == customer.Id && ba.IsActive
                        && ba.ValidFrom <= now && (ba.ValidTo == null || ba.ValidTo >= now))
                    .Include(ba => ba.BenefitDefinition)
                    .Where(ba => ba.BenefitDefinition.BenefitKind == AppliedBenefitKind.BuyXGetY
                        && ba.BenefitDefinition.IsActive
                        && ba.BenefitDefinition.BuyXQuantity.HasValue
                        && ba.BenefitDefinition.BuyXQuantity > 0
                        && ba.BenefitDefinition.GetYQuantity.HasValue
                        && ba.BenefitDefinition.GetYQuantity > 0)
                    .OrderByDescending(ba => ba.Priority)
                    .FirstOrDefaultAsync();

                if (buyXGetYAssignment?.BenefitDefinition != null)
                {
                    var def = buyXGetYAssignment.BenefitDefinition;
                    var buyX = def.BuyXQuantity!.Value;
                    var getY = def.GetYQuantity!.Value;
                    var totalQuantity = paymentItems.Sum(pi => pi.Quantity);
                    if (totalQuantity >= buyX)
                    {
                        var sets = totalQuantity / buyX;
                        var freeQty = Math.Min(sets * getY, totalQuantity);
                        if (freeQty > 0)
                        {
                            var unitPrices = new List<decimal>();
                            foreach (var pi in paymentItems)
                            {
                                for (var i = 0; i < pi.Quantity; i++)
                                    unitPrices.Add(pi.UnitPrice);
                            }
                            unitPrices.Sort();
                            var take = Math.Min(freeQty, unitPrices.Count);
                            var discountAmount = 0m;
                            for (var i = 0; i < take; i++)
                                discountAmount += unitPrices[i];
                            discountAmount = CartMoneyHelper.Round(discountAmount);
                            if (discountAmount > 0)
                            {
                                var totalBeforeBuyXGetY = totalAmount;
                                totalAmount -= discountAmount;
                                var ratio = totalBeforeBuyXGetY > 0 ? totalAmount / totalBeforeBuyXGetY : 1m;
                                totalTaxAmount = CartMoneyHelper.Round(totalTaxAmount * ratio);
                                var keysBxg = taxDetails.Keys.ToList();
                                foreach (var key in keysBxg)
                                    taxDetails[key] = CartMoneyHelper.Round(taxDetails[key] * ratio);
                                snapshotList.Add(new AppliedBenefitSnapshotItem
                                {
                                    Kind = AppliedBenefitKind.BuyXGetY,
                                    Description = $"Buy {buyX} get {getY} free ({freeQty} items)",
                                    Amount = -discountAmount,
                                    Quantity = freeQty
                                });
                            }
                        }
                    }
                }

                if (snapshotList.Count > 0)
                    appliedBenefitsSnapshot = JsonDocument.Parse(JsonSerializer.Serialize(snapshotList));

                // CashRegisterId çözümle (TSE imzası için)
                Guid resolvedCashRegisterId = Guid.Empty;
                if (!string.IsNullOrWhiteSpace(request.KassenId))
                {
                    if (Guid.TryParse(request.KassenId, out var parsedRegId))
                    {
                        var crExists = await _context.CashRegisters.AnyAsync(cr => cr.Id == parsedRegId);
                        if (crExists) resolvedCashRegisterId = parsedRegId;
                    }
                    if (resolvedCashRegisterId == Guid.Empty)
                    {
                        var register = await _context.CashRegisters.FirstOrDefaultAsync(cr => cr.RegisterNumber == request.KassenId);
                        resolvedCashRegisterId = register?.Id ?? Guid.Empty;
                    }
                }

                var seq = Guid.NewGuid().ToString("N")[..8];
                var preReceiptNumber = $"AT-{request.KassenId}-{DateTime.UtcNow:yyyyMMdd}-{seq}";

                // Ödeme detayları oluştur (totals = CartMoneyHelper toplamları; optional customer % discount already applied above)
                var payment = new PaymentDetails
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    PaymentItems = JsonDocument.Parse(JsonSerializer.Serialize(paymentItems)),
                    TotalAmount = totalAmount,
                    TaxAmount = totalTaxAmount,
                    TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(taxDetails)),
                    PaymentMethodRaw = GetPaymentMethodEnum(request.Payment.Method),
                    Notes = request.Notes,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    TableNumber = request.TableNumber,
                    // Single source: authenticated user only (validated above; placeholders rejected or normalized).
                    CashierId = userId,
                    Steuernummer = request.Steuernummer,
                    KassenId = request.KassenId,
                    TseTimestamp = DateTime.UtcNow,
                    IsPrinted = false,
                    ReceiptNumber = preReceiptNumber,
                    AppliedBenefitsSnapshot = appliedBenefitsSnapshot,
                    IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim()
                };

                // TSE imzası oluştur (eğer gerekliyse) - RKSV Checklist 1-5 uyumlu COMPACT JWS
                if (effectiveTseRequired)
                {
                    try
                    {
                        var sigResult = await _tseService.CreateInvoiceSignatureAsync(
                            resolvedCashRegisterId != Guid.Empty ? resolvedCashRegisterId : payment.Id,
                            preReceiptNumber,
                            payment.TotalAmount,
                            kassenId: request.KassenId,
                            taxDetailsJson: JsonSerializer.Serialize(taxDetails));
                        payment.TseSignature = sigResult.CompactJws;
                        payment.PrevSignatureValueUsed = sigResult.PrevSignatureValueUsed;
                        _logger.LogInformation("TSE signature generated for payment {PaymentId}", payment.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate TSE signature for payment {PaymentId}", payment.Id);
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "Failed to generate TSE signature",
                            Errors = { "TSE signature generation failed" }
                        };
                    }
                }

                // Persist payment and any tracked changes (e.g. BenefitDailyUsage). Concurrency conflict possible when daily allowance is consumed at another terminal.
                PaymentDetails createdPayment;
                try
                {
                    createdPayment = await _paymentRepository.AddAsync(payment);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "Daily allowance concurrency conflict for customer {CustomerId} during payment creation", request.CustomerId);
                    return ToDailyAllowanceConflictResult();
                }
                catch (DbUpdateException ex) when (IsIdempotencyKeyViolation(ex))
                {
                    if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                    {
                        var existing = await _context.PaymentDetails.AsNoTracking()
                            .FirstOrDefaultAsync(p => p.IdempotencyKey == request.IdempotencyKey);
                        if (existing != null)
                        {
                            _logger.LogInformation("Idempotency key race: returning existing payment {PaymentId} for key {Key}", existing.Id, request.IdempotencyKey);
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
                                TseProvider = provider
                            };
                        }
                    }
                    throw;
                }
                catch (DbUpdateException ex) when (IsBenefitDailyUsageConflict(ex))
                {
                    _logger.LogWarning(ex, "Daily allowance unique constraint race for customer {CustomerId} during payment creation", request.CustomerId);
                    return ToDailyAllowanceConflictResult();
                }

                // Persist a canonical Invoice row so that list / detail / PDF all share the same domain and ID
                try
                {
                    // Idempotency check: skip if an invoice already exists for this payment
                    var existingInvoice = await _context.Invoices
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.SourcePaymentId == createdPayment.Id);

                    if (existingInvoice == null)
                    {
                        var companyAddress = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}";
                        Guid? invoiceCashRegisterId = resolvedCashRegisterId != Guid.Empty ? resolvedCashRegisterId : null;
                        if (invoiceCashRegisterId == null && !string.IsNullOrWhiteSpace(createdPayment.KassenId))
                        {
                            _logger.LogWarning("Could not resolve real CashRegisterId for KassenId '{KassenId}' during payment invoice creation. Saving without CashRegisterId, Tagesabschluss may miss this.", createdPayment.KassenId);
                        }

                        var posInvoice = new Invoice
                        {
                            Id = Guid.NewGuid(),
                            SourcePaymentId = createdPayment.Id,
                            InvoiceNumber = createdPayment.ReceiptNumber,
                            InvoiceDate = createdPayment.CreatedAt,
                            DueDate = createdPayment.CreatedAt,
                            Status = InvoiceStatus.Paid,
                            Subtotal = createdPayment.TotalAmount - createdPayment.TaxAmount,
                            TaxAmount = createdPayment.TaxAmount,
                            TotalAmount = createdPayment.TotalAmount,
                            PaidAmount = createdPayment.TotalAmount,
                            RemainingAmount = 0,
                            CustomerName = createdPayment.CustomerName,
                            CustomerTaxNumber = createdPayment.Steuernummer,
                            CompanyName = _companyProfile.CompanyName,
                            CompanyTaxNumber = _companyProfile.TaxNumber,
                            CompanyAddress = companyAddress,
                            TseSignature = createdPayment.TseSignature ?? string.Empty,
                            KassenId = createdPayment.KassenId,
                            TseTimestamp = createdPayment.TseTimestamp,
                            CashRegisterId = invoiceCashRegisterId,
                            PaymentMethod = createdPayment.PaymentMethod,
                            PaymentReference = createdPayment.TransactionId,
                            PaymentDate = createdPayment.CreatedAt,
                            InvoiceItems = createdPayment.PaymentItems,
                            TaxDetails = createdPayment.TaxDetails,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        _context.Invoices.Add(posInvoice);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Invoice persisted for payment {PaymentId}: InvoiceId={InvoiceId}, InvoiceNumber={InvoiceNumber}",
                            createdPayment.Id, posInvoice.Id, posInvoice.InvoiceNumber);

                        // FinanzOnline'a gönder (TSE gerekiyorsa)
                        if (effectiveTseRequired)
                        {
                            try
                            {
                                await _finanzOnlineService.SubmitInvoiceAsync(posInvoice);
                                _logger.LogInformation("Invoice sent to FinanzOnline: {InvoiceId}", posInvoice.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to send invoice to FinanzOnline: {InvoiceId}", posInvoice.Id);
                                // FinanzOnline hatası ödeme oluşturmayı engellemez
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Invoice already exists for payment {PaymentId} — skipping duplicate insert", createdPayment.Id);
                    }
                }
                catch (Exception ex)
                {
                    // Invoice persist hatası ödeme oluşturmayı engellemez — PaymentDetails kaydı zaten var
                    _logger.LogError(ex, "Failed to persist Invoice for payment {PaymentId}", createdPayment.Id);
                }

                _logger.LogInformation("Payment created successfully: {PaymentId} for customer {CustomerId}", 
                    createdPayment.Id, customer.Id);

                // QR payload: RKSV belegdaten veya NON_FISCAL_DEMO
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
                    TseProvider = tseProvider
                };
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
        /// QR payload (RKSV belegdaten veya NON_FISCAL_DEMO) ve demo/fiscal flag'leri üretir.
        /// tseRequired=false: Açık NON_FISCAL marker ile UI yanlışlıkla fiskal sanmasın.
        /// </summary>
        private async Task<(string QrPayload, bool IsDemoFiscal, string TseProvider)> BuildQrPayloadAndFlagsAsync(PaymentDetails payment, bool tseRequired)
        {
            var isDemoFiscal = !tseRequired || _tseOptions.UseSoftTseWhenNoDevice;
            var tseProvider = tseRequired ? (_tseOptions.UseSoftTseWhenNoDevice ? "Demo" : "Device") : "None";
            var kassenId = payment.KassenId ?? "";
            var receiptNumber = payment.ReceiptNumber ?? "";
            var createdAt = payment.CreatedAt;
            var totalAmount = payment.TotalAmount;
            var signatureValue = payment.TseSignature ?? "";

            string qrPayload;
            if (!string.IsNullOrEmpty(signatureValue))
            {
                var certInfo = await _tseService.GetTseCertificateInfoAsync(kassenId);
                var certSerial = certInfo.CertificateNumber ?? "DEMO-CERT";
                qrPayload = $"_R1-AT1_{kassenId}_{receiptNumber}_{createdAt:yyyy-MM-ddTHH:mm:ss}_{totalAmount:F2}_0.00_{certSerial}_{signatureValue}";
            }
            else
            {
                // tseRequired=false: Açık NON_FISCAL marker (sadece flag değil) - UI fiskal sanmasın
                qrPayload = $"NON_FISCAL_DEMO_{receiptNumber}_{createdAt:yyyy-MM-ddTHH:mm:ss}_{totalAmount:F2}";
            }

            return (qrPayload, isDemoFiscal, tseProvider);
        }

        /// <summary>
        /// Ödeme için QR payload üretir. Aynı mantık BuildQrPayloadAndFlagsAsync ile.
        /// </summary>
        public async Task<(string? QrPayload, DateTime? UpdatedAt)?> GetQrPayloadForPaymentAsync(Guid paymentId)
        {
            var payment = await GetPaymentAsync(paymentId);
            if (payment == null) return null;
            var effectiveTseRequired = !string.IsNullOrEmpty(payment.TseSignature);
            var (qrPayload, _, _) = await BuildQrPayloadAndFlagsAsync(payment, effectiveTseRequired);
            var updatedAt = payment.UpdatedAt ?? payment.CreatedAt;
            return (qrPayload, updatedAt);
        }

        /// <summary>
        /// Ödeme detaylarını getir
        /// </summary>
        public async Task<PaymentDetails?> GetPaymentAsync(Guid paymentId)
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    _logger.LogDebug("Payment not found with ID: {PaymentId}", paymentId);
                    return null;
                }

                _logger.LogDebug("Payment retrieved successfully: {PaymentId}", paymentId);
                return payment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment {PaymentId}", paymentId);
                return null;
            }
        }

        /// <summary>
        /// Müşteri ödemelerini getir
        /// </summary>
        public async Task<IEnumerable<PaymentDetails>> GetCustomerPaymentsAsync(Guid customerId, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                // Sayfa boyutu validasyonu
                if (pageSize <= 0 || pageSize > 100)
                {
                    _logger.LogWarning("Invalid page size: {PageSize}. Using default value 20", pageSize);
                    pageSize = 20;
                }

                if (pageNumber <= 0)
                {
                    _logger.LogWarning("Invalid page number: {PageNumber}. Using default value 1", pageNumber);
                    pageNumber = 1;
                }

                var (items, totalCount) = await _paymentRepository.GetPagedAsync(
                    pageNumber, 
                    pageSize, 
                    p => p.CustomerId == customerId && p.IsActive,
                    p => p.CreatedAt,
                    false);

                _logger.LogDebug("Retrieved {Count} payments for customer {CustomerId} (page {PageNumber}/{TotalPages})", 
                    items.Count(), customerId, pageNumber, Math.Ceiling((double)totalCount / pageSize));

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments for customer {CustomerId}", customerId);
                return Enumerable.Empty<PaymentDetails>();
            }
        }

        /// <summary>
        /// Ödeme yöntemine göre ödemeleri getir
        /// </summary>
        public async Task<IEnumerable<PaymentDetails>> GetPaymentsByMethodAsync(string paymentMethod, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                // Ödeme yöntemi validasyonu
                var validMethods = new[] { "cash", "card", "voucher" };
                if (!validMethods.Contains(paymentMethod.ToLower()))
                {
                    _logger.LogWarning("Invalid payment method: {PaymentMethod}", paymentMethod);
                    return Enumerable.Empty<PaymentDetails>();
                }

                // Sayfa boyutu validasyonu
                if (pageSize <= 0 || pageSize > 100)
                {
                    _logger.LogWarning("Invalid page size: {PageSize}. Using default value 20", pageSize);
                    pageSize = 20;
                }

                if (pageNumber <= 0)
                {
                    _logger.LogWarning("Invalid page number: {PageNumber}. Using default value 1", pageNumber);
                    pageNumber = 1;
                }

                var (items, totalCount) = await _paymentRepository.GetPagedAsync(
                    pageNumber, 
                    pageSize, 
                    p => p.PaymentMethodRaw == GetPaymentMethodEnum(paymentMethod) && p.IsActive,
                    p => p.CreatedAt,
                    false);

                _logger.LogDebug("Retrieved {Count} payments for method {PaymentMethod} (page {PageNumber}/{TotalPages})", 
                    items.Count(), paymentMethod, pageNumber, Math.Ceiling((double)totalCount / pageSize));

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments by method {PaymentMethod}", paymentMethod);
                return Enumerable.Empty<PaymentDetails>();
            }
        }

        /// <summary>
        /// Tarih aralığına göre ödemeleri getir
        /// </summary>
        public async Task<IEnumerable<PaymentDetails>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                // Tarih validasyonu
                if (startDate > endDate)
                {
                    _logger.LogWarning("Invalid date range: startDate {StartDate} is after endDate {EndDate}", startDate, endDate);
                    return Enumerable.Empty<PaymentDetails>();
                }

                // Maksimum tarih aralığı kontrolü (7 yıl)
                var maxDateRange = TimeSpan.FromDays(365 * 7);
                if (endDate - startDate > maxDateRange)
                {
                    _logger.LogWarning("Date range too large: {Days} days. Maximum allowed: {MaxDays} days", 
                        (endDate - startDate).Days, maxDateRange.Days);
                    return Enumerable.Empty<PaymentDetails>();
                }

                // Sayfa boyutu validasyonu
                if (pageSize <= 0 || pageSize > 100)
                {
                    _logger.LogWarning("Invalid page size: {PageSize}. Using default value 20", pageSize);
                    pageSize = 20;
                }

                if (pageNumber <= 0)
                {
                    _logger.LogWarning("Invalid page number: {PageNumber}. Using default value 1", pageNumber);
                    pageNumber = 1;
                }

                var (items, totalCount) = await _paymentRepository.GetPagedAsync(
                    pageNumber, 
                    pageSize, 
                    p => p.CreatedAt >= startDate && p.CreatedAt <= endDate && p.IsActive,
                    p => p.CreatedAt,
                    false);

                _logger.LogDebug("Retrieved {Count} payments for date range {StartDate} to {EndDate} (page {PageNumber}/{TotalPages})", 
                    items.Count(), startDate, endDate, pageNumber, Math.Ceiling((double)totalCount / pageSize));

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments by date range {StartDate} to {EndDate}", startDate, endDate);
                return Enumerable.Empty<PaymentDetails>();
            }
        }

        /// <summary>
        /// Ödeme iptal et
        /// </summary>
        public async Task<PaymentResult> CancelPaymentAsync(Guid paymentId, string reason, string userId)
        {
            try
            {
                // Demo kullanıcı kontrolü: IsDemo bayrağı (ve eski Demo rolü geriye dönük uyumluluk için)
                var user = await _userService.GetUserByIdAsync(userId);
                if (DemoUserHelper.IsDemoUser(user))
                {
                    var rejectionReason = DemoUserHelper.GetDemoRejectionReason(user) ?? "DEMO_UNKNOWN";
                    _logger.LogWarning(
                        "Payment cancel demo rejection: AuthenticatedUserId={AuthenticatedUserId} AuthenticatedUserEmail={AuthenticatedUserEmail} PayloadCashierId={PayloadCashierId} ResolvedUserId={ResolvedUserId} ResolvedUserEmail={ResolvedUserEmail} ResolvedUserRole={ResolvedUserRole} ResolvedUserIsDemo={ResolvedUserIsDemo} RejectionCode={RejectionCode} PaymentId={PaymentId}",
                        userId,
                        user?.Email ?? "",
                        "",
                        user?.Id ?? "",
                        user?.Email ?? "",
                        user?.Role ?? "",
                        user?.IsDemo ?? false,
                        rejectionReason,
                        paymentId);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Demo users cannot cancel real payments",
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

                // Zaten iptal edilmiş ödeme kontrolü
                if (!payment.IsActive)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment is already cancelled",
                        Errors = { "Payment has already been cancelled" }
                    };
                }

                // TSE imzası varsa iptal et
                if (!string.IsNullOrEmpty(payment.TseSignature))
                {
                    try
                    {
                        await _tseService.CancelInvoiceSignatureAsync(payment.TseSignature);
                        _logger.LogInformation("TSE signature cancelled for payment {PaymentId}", paymentId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cancel TSE signature for payment {PaymentId}", paymentId);
                        // TSE imza iptali başarısız olsa bile ödeme iptal edilebilir
                    }
                }

                // Stok geri ekle - transaction içinde yapılmalı
                var paymentItems = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.RootElement.GetRawText());
                if (paymentItems != null)
                {
                    foreach (var item in paymentItems)
                    {
                        var product = await _productRepository.GetByIdAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;
                            await _productRepository.UpdateAsync(product);
                        }
                    }
                }

                // Ödemeyi iptal et
                payment.IsActive = false;
                payment.UpdatedAt = DateTime.UtcNow;
                payment.UpdatedBy = userId;
                // TODO: CancellationReason ve CancelledAt alanları eklenecek

                await _paymentRepository.UpdateAsync(payment);

                _logger.LogInformation("Payment {PaymentId} cancelled by user {UserId} with reason: {Reason}", 
                    paymentId, userId, reason);

                return new PaymentResult
                {
                    Success = true,
                    Message = "Payment cancelled successfully",
                    Payment = payment
                };
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

        /// <summary>
        /// Ödeme iade et
        /// </summary>
        public async Task<PaymentResult> RefundPaymentAsync(Guid paymentId, decimal amount, string reason, string userId)
        {
            try
            {
                // Demo kullanıcı kontrolü: IsDemo bayrağı (ve eski Demo rolü geriye dönük uyumluluk için)
                var user = await _userService.GetUserByIdAsync(userId);
                if (DemoUserHelper.IsDemoUser(user))
                {
                    var rejectionReason = DemoUserHelper.GetDemoRejectionReason(user) ?? "DEMO_UNKNOWN";
                    _logger.LogWarning(
                        "Payment refund demo rejection: AuthenticatedUserId={AuthenticatedUserId} AuthenticatedUserEmail={AuthenticatedUserEmail} PayloadCashierId={PayloadCashierId} ResolvedUserId={ResolvedUserId} ResolvedUserEmail={ResolvedUserEmail} ResolvedUserRole={ResolvedUserRole} ResolvedUserIsDemo={ResolvedUserIsDemo} RejectionCode={RejectionCode} PaymentId={PaymentId}",
                        userId,
                        user?.Email ?? "",
                        "",
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

                // Zaten iptal edilmiş ödeme kontrolü
                if (!payment.IsActive)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Cannot refund cancelled payment",
                        Errors = { "Payment has been cancelled and cannot be refunded" }
                    };
                }

                // İade tutarı kontrolü
                if (amount > payment.TotalAmount)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Refund amount cannot exceed payment amount",
                        Errors = { "Refund amount exceeds payment amount" }
                    };
                }

                // Kısmi iade için stok güncelleme
                if (amount < payment.TotalAmount)
                {
                    var refundRatio = amount / payment.TotalAmount;
                    var paymentItems = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.RootElement.GetRawText());
                    if (paymentItems != null)
                    {
                        foreach (var item in paymentItems)
                        {
                            var product = await _productRepository.GetByIdAsync(item.ProductId);
                            if (product != null)
                            {
                                var refundQuantity = (int)(item.Quantity * refundRatio);
                                product.StockQuantity += refundQuantity;
                                product.UpdatedAt = DateTime.UtcNow;
                                await _productRepository.UpdateAsync(product);
                            }
                        }
                    }
                }

                // İade kaydı oluştur
                var refund = new PaymentDetails
                {
                    CustomerId = payment.CustomerId,
                    CustomerName = payment.CustomerName,
                    PaymentItems = payment.PaymentItems, // JSON olarak kopyala
                    TotalAmount = -amount, // Negatif tutar
                    TaxAmount = -payment.TaxAmount * (amount / payment.TotalAmount),
                    PaymentMethod = payment.PaymentMethod,
                    Notes = $"Refund: {reason}",
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                    // TODO: IsRefund, OriginalPaymentId, RefundReason, RefundAmount alanları eklenecek
                };

                await _paymentRepository.AddAsync(refund);

                _logger.LogInformation("Refund created for payment {PaymentId} by user {UserId} for amount {Amount}", 
                    paymentId, userId, amount);

                return new PaymentResult
                {
                    Success = true,
                    Message = "Refund processed successfully",
                    Payment = refund
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

        /// <summary>
        /// Get payment statistics for date range
        /// </summary>
        public async Task<PaymentStatistics> GetPaymentStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            // Initialize DTO with request dates immediately (never returns DateTime.MinValue)
            var statistics = new PaymentStatistics
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalPayments = 0,
                TotalAmount = 0,
                AverageAmount = 0,
                PaymentsByMethod = new Dictionary<string, int>(),
                AmountByMethod = new Dictionary<string, decimal>(),
                PaymentsByTaxType = new Dictionary<string, int>(),
                TseSignedPayments = 0,
                TseSignedAmount = 0
            };

            // Query using PaymentMethodRaw - no InvalidCastException since it's varchar
            var payments = await _context.PaymentDetails
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate && p.IsActive)
                .ToListAsync();

            // Diagnostic log to confirm PaymentMethodRaw reads as string
            if (payments.Any())
            {
                var firstPayment = payments.First();
                _logger.LogInformation("DIAGNOSTIC: PaymentMethodRaw type={Type}, value={Value}", 
                    firstPayment.PaymentMethodRaw?.GetType().Name ?? "null", 
                    firstPayment.PaymentMethodRaw ?? "null");
            }

            if (!payments.Any())
            {
                _logger.LogInformation("No payments found for period {StartDate} to {EndDate}", startDate, endDate);
                return statistics;
            }

            // Calculate basic statistics
            statistics.TotalPayments = payments.Count;
            statistics.TotalAmount = payments.Sum(p => p.TotalAmount);
            statistics.AverageAmount = payments.Average(p => p.TotalAmount);

            // Group by payment method - parse varchar numeric strings to enum names
            var paymentMethodGroups = payments
                .Select(p => new 
                { 
                    Payment = p,
                    MethodName = ParsePaymentMethodName(p.PaymentMethodRaw)
                })
                .GroupBy(x => x.MethodName)
                .ToList();

            statistics.PaymentsByMethod = paymentMethodGroups
                .ToDictionary(g => g.Key, g => g.Count());

            statistics.AmountByMethod = paymentMethodGroups
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Payment.TotalAmount));

            // Group by tax type (safe JSON parsing)
            foreach (var payment in payments)
            {
                try 
                {
                    if (payment.PaymentItems != null && payment.PaymentItems.RootElement.ValueKind != JsonValueKind.Undefined)
                    {
                        var jsonText = payment.PaymentItems.RootElement.GetRawText();
                        var paymentItems = JsonSerializer.Deserialize<List<PaymentItem>>(jsonText);
                        
                        if (paymentItems != null)
                        {
                            foreach (var item in paymentItems)
                            {
                                var taxType = item.TaxType.ToString();
                                if (!statistics.PaymentsByTaxType.ContainsKey(taxType))
                                    statistics.PaymentsByTaxType[taxType] = 0;
                                statistics.PaymentsByTaxType[taxType]++;
                            }
                        }
                    }
                }
                catch (Exception jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Failed to deserialize payment items for payment {PaymentId}", payment.Id);
                }
            }

            // TSE statistics
            var tsePayments = payments.Where(p => !string.IsNullOrEmpty(p.TseSignature)).ToList();
            statistics.TseSignedPayments = tsePayments.Count;
            statistics.TseSignedAmount = tsePayments.Sum(p => p.TotalAmount);

            _logger.LogInformation("Payment statistics: {StartDate} to {EndDate}, {Count} payments, {Amount} total", 
                startDate, endDate, statistics.TotalPayments, statistics.TotalAmount);

            return statistics;
            // No try-catch - exceptions propagate to controller for proper 500 response
        }

        /// <summary>
        /// Parse varchar payment method string ('0', '1', etc.) to readable enum name
        /// </summary>
        private string ParsePaymentMethodName(string rawValue)
        {
            if (int.TryParse(rawValue, out int methodInt) && Enum.IsDefined(typeof(PaymentMethod), methodInt))
            {
                return ((PaymentMethod)methodInt).ToString();
            }
            return "Unknown";
        }

        /// <summary>
        /// TSE imzası oluştur
        /// </summary>
        public async Task<string> GenerateTseSignatureAsync(PaymentDetails payment)
        {
            try
            {
                // TseMode=Device iken cihaz kontrolü; Demo modda atlanır
                if (!_tseOptions.UseSoftTseWhenNoDevice)
                {
                var tseStatus = await _tseService.GetDeviceStatusAsync();
                if (!tseStatus.IsConnected)
                {
                    _logger.LogError("TSE device not connected. Cannot generate signature for payment {PaymentId}", payment.Id);
                    throw new InvalidOperationException("TSE device is not connected");
                }

                // TSE cihazı hazır mı kontrolü
                if (!tseStatus.IsReady)
                {
                    _logger.LogWarning("TSE device not ready. Status: {Status}", tseStatus.Status);
                    throw new InvalidOperationException($"TSE device is not ready. Status: {tseStatus.Status}");
                }
                }

                Guid cashRegisterId = Guid.Empty;
                if (!string.IsNullOrWhiteSpace(payment.KassenId))
                {
                    if (Guid.TryParse(payment.KassenId, out var parsed)) cashRegisterId = parsed;
                    else
                    {
                        var reg = await _context.CashRegisters.FirstOrDefaultAsync(cr => cr.RegisterNumber == payment.KassenId);
                        cashRegisterId = reg?.Id ?? Guid.Empty;
                    }
                }
                if (cashRegisterId == Guid.Empty) cashRegisterId = payment.Id;

                var sigResult = await _tseService.CreateInvoiceSignatureAsync(
                    cashRegisterId,
                    payment.ReceiptNumber ?? payment.Id.ToString(),
                    payment.TotalAmount,
                    kassenId: payment.KassenId,
                    taxDetailsJson: payment.TaxDetails?.RootElement.GetRawText());
                var signature = sigResult.CompactJws;

                _logger.LogInformation("TSE signature generated successfully for payment {PaymentId}: {Signature}", 
                    payment.Id, signature);

                return signature;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating TSE signature for payment {PaymentId}", payment.Id);
                throw;
            }
        }

        /// <summary>
        /// FinanzOnline entegrasyonu
        /// </summary>
        public async Task<bool> SendToFinanzOnlineAsync(PaymentDetails payment)
        {
            try
            {
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
                    CompanyName = "Company Name", // Gerçek implementasyonda config'den alınmalı
                    CompanyTaxNumber = "ATU12345678", // Gerçek implementasyonda config'den alınmalı
                    CompanyAddress = "Company Address", // Gerçek implementasyonda config'den alınmalı
                    TseSignature = payment.TseSignature,
                    KassenId = "KASSE001", // Gerçek implementasyonda config'den alınmalı
                    TseTimestamp = payment.CreatedAt,
                    CashRegisterId = Guid.NewGuid(), // Gerçek implementasyonda config'den alınmalı
                    PaymentMethod = payment.PaymentMethod,
                    PaymentReference = payment.TransactionId,
                    PaymentDate = payment.CreatedAt
                };

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

        /// <summary>
        /// Get formatted receipt data for payment
        /// </summary>
        public async Task<ReceiptDTO?> GetReceiptDataAsync(Guid paymentId)
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    _logger.LogWarning("Payment not found for receipt: {PaymentId}", paymentId);
                    return null;
                }

                // Deserialize payment items
                var paymentItems = JsonSerializer.Deserialize<List<PaymentItem>>(
                    payment.PaymentItems.RootElement.GetRawText());

                if (paymentItems == null)
                {
                    _logger.LogError("Failed to deserialize payment items for payment {PaymentId}", paymentId);
                    return null;
                }

                // Get cashier name
                var cashier = await _userService.GetUserByIdAsync(payment.CreatedBy);
                var cashierName = cashier?.Name ?? cashier?.UserName ?? "Unknown";

                // Phase 2 receipt: Prefer flat lines (one ReceiptItemDTO per PaymentItem). Legacy: product + embedded Modifiers → main line + nested modifier lines.
                var receiptItems = new List<ReceiptItemDTO>();
                var legacySnapshotItemCount = 0;
                foreach (var item in paymentItems)
                {
                    var mainItemId = Guid.NewGuid();
                    var hasLegacyModifiers = item.Modifiers != null && item.Modifiers.Count > 0;
                    if (hasLegacyModifiers)
                        legacySnapshotItemCount++;
                    var modifierNet = item.Modifiers?.Sum(m => m.LineNet) ?? 0;
                    var modifierGross = item.Modifiers?.Sum(m => m.TotalPrice) ?? 0;
                    var modifierTax = item.Modifiers?.Sum(m => m.TaxAmount) ?? 0;

                    // Flat path (new): product-only line — one receipt line with full totals.
                    // Legacy path: main line shows base only (totals minus modifiers); modifier lines added below.
                    receiptItems.Add(new ReceiptItemDTO
                    {
                        ItemId = mainItemId,
                        Name = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = hasLegacyModifiers ? item.TotalPrice - modifierGross : item.TotalPrice,
                        LineTotalNet = hasLegacyModifiers ? item.LineNet - modifierNet : item.LineNet,
                        LineTotalGross = hasLegacyModifiers ? item.TotalPrice - modifierGross : item.TotalPrice,
                        TaxRate = item.TaxRate * 100,
                        VatRate = item.TaxRate,
                        VatAmount = hasLegacyModifiers ? item.TaxAmount - modifierTax : item.TaxAmount,
                        CategoryName = null,
                        ParentItemId = null,
                        IsModifierLine = false
                    });
                    // Legacy only: nested modifier lines (historical receipts keep parent + child display).
                    if (hasLegacyModifiers)
                    {
                        foreach (var m in item.Modifiers!)
                        {
                            receiptItems.Add(new ReceiptItemDTO
                            {
                                ItemId = Guid.NewGuid(),
                                Name = "+ " + m.Name,
                                Quantity = item.Quantity,
                                UnitPrice = m.UnitPrice,
                                TotalPrice = m.TotalPrice,
                                LineTotalNet = m.LineNet,
                                LineTotalGross = m.TotalPrice,
                                TaxRate = m.TaxRate * 100,
                                VatRate = m.TaxRate,
                                VatAmount = m.TaxAmount,
                                CategoryName = null,
                                ParentItemId = mainItemId,
                                IsModifierLine = true
                            });
                        }
                    }
                }

                // Phase 2 observability: when this log stops appearing, no receipts are rendered from PaymentItem.Modifiers (legacy snapshots) anymore.
                if (legacySnapshotItemCount > 0)
                    _logger.LogInformation("Phase2.LegacyModifier.ReceiptRenderedFromLegacyModifierSnapshots PaymentId={PaymentId} ItemsWithLegacyModifiersCount={ItemsWithLegacyModifiersCount}", payment.Id, legacySnapshotItemCount);

                // Applied benefits: add synthetic receipt lines from snapshot so Items sum matches payment.TotalAmount (discounted).
                List<AppliedBenefitSnapshotItem>? snapshotItems = null;
                if (payment.AppliedBenefitsSnapshot != null)
                {
                    try
                    {
                        snapshotItems = JsonSerializer.Deserialize<List<AppliedBenefitSnapshotItem>>(payment.AppliedBenefitsSnapshot.RootElement.GetRawText());
                    }
                    catch
                    {
                        _logger.LogWarning("Failed to parse AppliedBenefitsSnapshot for payment {PaymentId}; receipt will not include benefit lines.", paymentId);
                    }
                }
                decimal benefitTaxAmount = 0;
                decimal totalBenefitAmount = 0;
                if (snapshotItems != null && snapshotItems.Count > 0)
                {
                    var productTaxTotal = paymentItems.Sum(i => i.TaxAmount);
                    benefitTaxAmount = payment.TaxAmount - productTaxTotal;
                    totalBenefitAmount = snapshotItems.Sum(x => x.Amount);
                    var first = true;
                    foreach (var item in snapshotItems)
                    {
                        var vatAmount = first ? benefitTaxAmount : 0m;
                        var lineNet = first ? item.Amount - benefitTaxAmount : item.Amount;
                        first = false;
                        receiptItems.Add(new ReceiptItemDTO
                        {
                            ItemId = Guid.NewGuid(),
                            Name = item.Description,
                            Quantity = 1,
                            UnitPrice = item.Amount,
                            TotalPrice = item.Amount,
                            LineTotalNet = lineNet,
                            LineTotalGross = item.Amount,
                            TaxRate = 0,
                            VatRate = 0,
                            VatAmount = vatAmount,
                            CategoryName = null,
                            ParentItemId = null,
                            IsModifierLine = false
                        });
                    }
                }

                var subtotal = paymentItems.Sum(i => i.LineNet);

                // Receipt totals = payment totals; when AppliedBenefitsSnapshot exists, receiptItems include synthetic lines so sum matches.
                if (Math.Abs(payment.TaxAmount - receiptItems.Sum(i => i.VatAmount)) > 0.01m)
                    _logger.LogWarning("Receipt VAT mismatch: payment.TaxAmount={PaymentTax}, sum(receiptItems.VatAmount)={SumTax}", payment.TaxAmount, receiptItems.Sum(i => i.VatAmount));
                if (Math.Abs(payment.TotalAmount - receiptItems.Sum(i => i.TotalPrice)) > 0.01m)
                    _logger.LogWarning("Receipt gross mismatch: payment.TotalAmount={Total}, sum(receiptItems.TotalPrice)={SumGross}", payment.TotalAmount, receiptItems.Sum(i => i.TotalPrice));

                // Tax breakdown: group by (TaxType, TaxRate); then append benefit line if snapshot present so header sums match payment totals.
                var taxRates = paymentItems
                    .GroupBy(i => new { i.TaxType, i.TaxRate })
                    .Select(g =>
                    {
                        var taxAmount = g.Sum(x => x.TaxAmount);
                        var grossAmount = g.Sum(x => x.TotalPrice);
                        var netAmount = g.Sum(x => Math.Abs((x.LineNet + x.TaxAmount) - x.TotalPrice) <= 0.01m ? x.LineNet : (x.TotalPrice - x.TaxAmount));
                        var dto = new ReceiptTaxLineDTO
                        {
                            TaxType = g.Key.TaxType,
                            Rate = g.Key.TaxRate * 100,
                            VatRate = g.Key.TaxRate,
                            TaxAmount = taxAmount,
                            NetAmount = netAmount,
                            GrossAmount = grossAmount
                        };
                        var groupCheck = Math.Abs((dto.NetAmount + dto.TaxAmount) - dto.GrossAmount);
                        if (groupCheck > 0.01m)
                            _logger.LogWarning("Receipt tax group invariant: (Net+Tax)-Gross={Diff} for TaxType={TaxType} Rate={Rate}", groupCheck, g.Key.TaxType, g.Key.TaxRate);
                        return dto;
                    })
                    .OrderBy(t => t.Rate)
                    .ThenBy(t => t.TaxType)
                    .ToList();

                if (snapshotItems != null && snapshotItems.Count > 0)
                {
                    taxRates.Add(new ReceiptTaxLineDTO
                    {
                        TaxType = 0,
                        Rate = 0,
                        VatRate = 0,
                        NetAmount = totalBenefitAmount - benefitTaxAmount,
                        TaxAmount = benefitTaxAmount,
                        GrossAmount = totalBenefitAmount
                    });
                }

                var headerNet = taxRates.Sum(t => t.NetAmount);
                var headerTax = taxRates.Sum(t => t.TaxAmount);
                var headerGross = taxRates.Sum(t => t.GrossAmount);
                if (Math.Abs((headerNet + headerTax) - headerGross) > 0.01m)
                    _logger.LogWarning("Receipt header invariant: (SubTotal+TaxAmount)-GrandTotal={Diff}", (headerNet + headerTax) - headerGross);
                if (Math.Abs(headerGross - payment.TotalAmount) > 0.01m || Math.Abs(headerTax - payment.TaxAmount) > 0.01m)
                    _logger.LogWarning("Receipt totals vs payment: receipt gross={RGross} tax={RTax}, payment total={PTotal} tax={PTax}", headerGross, headerTax, payment.TotalAmount, payment.TaxAmount);

                var receiptDTO = new ReceiptDTO
                {
                    ReceiptId = payment.Id,
                    ReceiptNumber = payment.ReceiptNumber ?? "DRAFT",
                    Date = payment.CreatedAt,
                    CashierName = cashierName,
                    TableNumber = payment.TableNumber,
                    KassenID = payment.KassenId ?? "KASSE01",
                    
                    Company = new ReceiptCompanyDTO
                    {
                        Name = _companyProfile.CompanyName,
                        Address = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}",
                        TaxNumber = !string.IsNullOrEmpty(payment.Steuernummer) ? payment.Steuernummer : _companyProfile.TaxNumber
                    },
                    
                    Header = new ReceiptHeaderDTO
                    {
                        ShopName = _companyProfile.CompanyName,
                        Address = $"{_companyProfile.Street}, {_companyProfile.City}"
                    },

                    Items = receiptItems,
                    
                    SubTotal = subtotal,
                    TaxAmount = payment.TaxAmount,
                    GrandTotal = payment.TotalAmount,
                    Totals = new ReceiptTotalsDTO
                    {
                        TotalNet = subtotal,
                        TotalVat = payment.TaxAmount,
                        TotalGross = payment.TotalAmount
                    },
                    
                    TaxRates = taxRates,
                    
                    Payments = new List<ReceiptPaymentDTO>
                    {
                        new ReceiptPaymentDTO
                        {
                            Method = payment.PaymentMethodRaw.ToString(),
                            Amount = payment.TotalAmount,
                            Tendered = payment.TotalAmount, // Assuming exact amount for now
                            Change = 0
                        }
                    },
                    
                    FooterText = _companyProfile.FooterText,
                    
                    Signature = !string.IsNullOrEmpty(payment.TseSignature) ? new ReceiptSignatureDTO
                    {
                        Algorithm = "ES256",
                        SerialNumber = (await _tseService.GetTseCertificateInfoAsync(payment.KassenId ?? string.Empty)).CertificateNumber,
                        Timestamp = payment.TseTimestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                        PrevSignatureValue = payment.PrevSignatureValueUsed ?? "",
                        SignatureValue = payment.TseSignature,
                        QrData = payment.TseSignature
                    } : null
                };

                _logger.LogInformation("Receipt data generated for payment {PaymentId}", paymentId);
                return receiptDTO;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating receipt data for payment {PaymentId}", paymentId);
                return null;
            }
        }

        #region Private Methods

        private decimal GetTaxRate(int taxType)
        {
            return TaxTypes.GetTaxRate(taxType) / 100.0m; // Convert 20.0 to 0.20
        }

        /// <summary>
        /// Convert payment method string to DB format (numeric string)
        /// </summary>
        private string GetPaymentMethodEnum(string paymentMethod)
        {
            // Map common payment method strings to numeric strings
            return paymentMethod?.ToLower() switch
            {
                "cash" => "0",
                "card" => "1",
                "banktransfer" => "2",
                "transfer" => "2",
                "check" => "3",
                "voucher" => "4",
                "mobile" => "5",
                _ => "0" // Default to Cash
            };
        }

        private bool IsValidAustrianTaxNumber(string taxNumber)
        {
            // ATU formatı: ATU + 8 haneli sayı
            var pattern = @"^ATU\d{8}$";
            return Regex.IsMatch(taxNumber, pattern);
        }

        /// <summary>
        /// Returns the standard PaymentResult for daily allowance conflict (concurrency or unique-index race).
        /// Keeps contract stable for mobile POS.
        /// </summary>
        private static PaymentResult ToDailyAllowanceConflictResult()
        {
            return new PaymentResult
            {
                Success = false,
                Message = "Daily allowance was used at another terminal. Please try again.",
                DiagnosticCode = "BENEFIT_DAILY_ALLOWANCE_CONFLICT",
                Errors = { "Benefit daily allowance concurrency conflict" }
            };
        }

        /// <summary>
        /// True when the exception is a PostgreSQL unique constraint violation (23505) on the idempotency_key column.
        /// Used to return existing payment on concurrent duplicate key insert.
        /// </summary>
        private static bool IsIdempotencyKeyViolation(DbUpdateException ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e is PostgresException pg && pg.SqlState == "23505" &&
                    (pg.ConstraintName?.Contains("idempotency", StringComparison.OrdinalIgnoreCase) ?? false))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True when the exception is a PostgreSQL unique constraint violation (23505) on BenefitDailyUsage
        /// (CustomerId, BenefitDefinitionId, UsageDate) create-if-missing race. Excludes idempotency_key violations.
        /// </summary>
        private static bool IsBenefitDailyUsageConflict(DbUpdateException ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e is PostgresException pg && pg.SqlState == "23505" &&
                    (pg.ConstraintName?.Contains("benefit", StringComparison.OrdinalIgnoreCase) ?? false))
                    return true;
            }
            return false;
        }

        #endregion
    }
}
