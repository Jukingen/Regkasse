using System.Net.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Time;
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
        private readonly IReceiptSequenceService _receiptSequenceService;
        private readonly IReceiptService _receiptService;
        private readonly IAuditLogService _auditLogService;
        private readonly IFinanzOnlineMetrics? _finanzOnlineMetrics;
        private readonly ICashRegisterResolutionService _cashRegisterResolution;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PaymentService(
            AppDbContext context,
            IGenericRepository<PaymentDetails> paymentRepository,
            IGenericRepository<Product> productRepository,
            IGenericRepository<Customer> customerRepository,
            ITseService tseService,
            IFinanzOnlineService finanzOnlineService,
            IUserService userService,
            IProductModifierValidationService modifierValidation,
            IReceiptSequenceService receiptSequenceService,
            IReceiptService receiptService,
            IAuditLogService auditLogService,
            Microsoft.Extensions.Options.IOptions<CompanyProfileOptions> companyProfile,
            Microsoft.Extensions.Options.IOptions<TseOptions> tseOptions,
            ILogger<PaymentService> logger,
            ICashRegisterResolutionService cashRegisterResolution,
            IHttpContextAccessor httpContextAccessor,
            IFinanzOnlineMetrics? finanzOnlineMetrics = null)
        {
            _context = context;
            _paymentRepository = paymentRepository;
            _productRepository = productRepository;
            _customerRepository = customerRepository;
            _tseService = tseService;
            _finanzOnlineService = finanzOnlineService;
            _userService = userService;
            _modifierValidation = modifierValidation;
            _receiptSequenceService = receiptSequenceService;
            _receiptService = receiptService;
            _auditLogService = auditLogService;
            _companyProfile = companyProfile.Value;
            _tseOptions = tseOptions.Value;
            _logger = logger;
            _cashRegisterResolution = cashRegisterResolution;
            _httpContextAccessor = httpContextAccessor;
            _finanzOnlineMetrics = finanzOnlineMetrics;
        }

        /// <summary>
        /// Yeni ödeme oluştur
        /// </summary>
        public async Task<PaymentResult> CreatePaymentAsync(
            CreatePaymentRequest request,
            string userId,
            Guid? offlineTransactionId = null,
            Guid? offlineReplayBatchCorrelationId = null)
        {
            try
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

                var resolvedCustomerKind = CustomerKindResolver.ResolveFromCustomerId(customer.Id, request.CustomerKind);
                _logger.LogDebug(
                    "Payment create resolved CustomerKind={CustomerKind} for CustomerId={CustomerId}",
                    resolvedCustomerKind,
                    customer.Id);

                // Idempotency: normalized key; duplicate requests return existing payment (no duplicate creation). Enforced by unique index on idempotency_key.
                var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
                if (idempotencyKey != null)
                {
                    var existing = await _context.PaymentDetails
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);
                    if (existing != null)
                    {
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
                            InvoicePersisted = invoiceExists
                        };
                    }
                }

                var effectiveSteuernummer = !string.IsNullOrWhiteSpace(request.Steuernummer) && IsValidAustrianTaxNumber(request.Steuernummer!)
                    ? request.Steuernummer!.Trim()
                    : _companyProfile.TaxNumber;

                if (!IsValidAustrianTaxNumber(effectiveSteuernummer))
                {
                    _logger.LogWarning("Resolved Steuernummer invalid (check CompanyProfile:TaxNumber): {TaxNumber}", effectiveSteuernummer);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Invalid Austrian tax number format",
                        Errors = { "Steuernummer must be in ATU format (e.g., ATU12345678). Configure CompanyProfile:TaxNumber." }
                    };
                }

                if (request.CashRegisterId == Guid.Empty)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "CashRegisterId is required",
                        Errors = { "CashRegisterId must be a non-empty GUID referencing an authorized open cash register." },
                        DiagnosticCode = CashRegisterResolutionCodes.Required
                    };
                }

                var principal = _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
                // Cash register: only ValidatePaymentRegisterAsync here — no IPosCashRegisterReadinessService / nextAction check.
                // Shift and assignment rules are aligned with ensure-ready conflict policy; session DTO is client-only unless refreshed.
                // AppPermissions.CashRegisterView does not bypass another user's shift at payment time (CashRegisterShiftOccupancy.IsHeldByOtherUser first).
                var registerValidation = await _cashRegisterResolution.ValidatePaymentRegisterAsync(
                    userId,
                    request.CashRegisterId,
                    principal);
                if (!registerValidation.Ok)
                {
                    _logger.LogWarning(
                        "Payment rejected: cash register policy. UserId={UserId} RegisterId={RegisterId} Code={Code}",
                        userId,
                        request.CashRegisterId,
                        registerValidation.Code);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = registerValidation.Message,
                        Errors = { registerValidation.Message },
                        DiagnosticCode = registerValidation.Code
                    };
                }

                // Authoritative register id/number come from commit gate inside the fiscal transaction (row lock).

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

                Guid cashRegisterId;
                string registerNumber;

                PaymentDetails? committedPayment = null;
                Invoice? committedInvoice = null;

                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if (idempotencyKey != null)
                    {
                        var existingByKeyInTx = await _context.PaymentDetails.AsNoTracking()
                            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);
                        if (existingByKeyInTx != null)
                        {
                            var invoiceExistsInTx = await _context.Invoices.AsNoTracking()
                                .AnyAsync(i => i.SourcePaymentId == existingByKeyInTx.Id);
                            _logger.LogInformation("Idempotency: returning existing payment {PaymentId} for key {Key}", existingByKeyInTx.Id, idempotencyKey);
                            await transaction.RollbackAsync();
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
                                InvoicePersisted = invoiceExistsInTx
                            };
                        }
                    }

                    var registerCommitValidation = await _cashRegisterResolution.ValidatePaymentRegisterForCommitAsync(
                        userId,
                        request.CashRegisterId,
                        principal);
                    if (!registerCommitValidation.Ok)
                    {
                        await transaction.RollbackAsync();
                        _context.ChangeTracker.Clear();
                        _logger.LogWarning(
                            "Payment rejected at commit gate: cash register policy. UserId={UserId} RegisterId={RegisterId} Code={Code}",
                            userId,
                            request.CashRegisterId,
                            registerCommitValidation.Code);
                        return new PaymentResult
                        {
                            Success = false,
                            Message = registerCommitValidation.Message,
                            Errors = { registerCommitValidation.Message },
                            DiagnosticCode = registerCommitValidation.Code
                        };
                    }

                    cashRegisterId = registerCommitValidation.ResolvedRegisterId!.Value;
                    registerNumber = registerCommitValidation.RegisterNumber!;

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
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return new PaymentResult
                            {
                                Success = false,
                                Message = "Product not found",
                                Errors = { $"Product with ID {itemRequest.ProductId} not found" }
                            };
                        }

                        if (product.CategoryNavigation == null)
                        {
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return new PaymentResult
                            {
                                Success = false,
                                Message = "Product category missing",
                                Errors = { $"Product {product.Name} has no category" }
                            };
                        }

                        // Phase 2: Sellable add-ons are product-only payment lines; no stock deduction (stok düşülmez).
                        // Stock is updated in memory on tracked entities; persisted in a single transaction with payment/invoice/receipt (no per-product UpdateAsync).
                        if (!product.IsSellableAddOn)
                        {
                            if (product.StockQuantity < itemRequest.Quantity)
                            {
                                await transaction.RollbackAsync();
                                _context.ChangeTracker.Clear();
                                return new PaymentResult
                                {
                                    Success = false,
                                    Message = "Insufficient stock",
                                    Errors = { $"Insufficient stock for product {product.Name}" }
                                };
                            }
                            product.StockQuantity -= itemRequest.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;
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
                        && (ba.BenefitDefinition.AllowanceScope == null || ba.BenefitDefinition.AllowanceScope.ToLower() == "per_day"))
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

                // Fiscal total authority: `totalAmount` is computed from catalog lines (and benefits); `request.TotalAmount` is only a client parity probe — persisted rows use `totalAmount`.
                const decimal amountTolerance = 0.01m;
                if (Math.Abs(totalAmount - request.TotalAmount) > amountTolerance)
                {
                    _logger.LogWarning(
                        "Payment total mismatch: calculated={Calculated}, request={Request}. Rejecting for fiscal integrity.",
                        totalAmount, request.TotalAmount);
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Total amount mismatch between client and server calculation.",
                        Errors = { "Total amount mismatch between client and server calculation." }
                    };
                }

                // Single database transaction: register row lock + stock + BelegNr + payment + invoice + receipt commit together.
                PaymentDetails? payment = null;
                Invoice? posInvoice = null;

                    // Sprint 1: Allocate BelegNr within this transaction so it commits or rolls back with payment/invoice/receipt/stock.
                    var preReceiptNumber = await _receiptSequenceService.AllocateNextBelegNrInTransactionAsync(transaction, cashRegisterId, registerNumber, DateTime.UtcNow);

                    // Ödeme detayları oluştur (totals = CartMoneyHelper toplamları; optional customer % discount already applied above)
                    payment = new PaymentDetails
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
                        CashierId = userId,
                        Steuernummer = effectiveSteuernummer,
                        CashRegisterId = cashRegisterId,
                        TseTimestamp = DateTime.UtcNow,
                        IsPrinted = false,
                        ReceiptNumber = preReceiptNumber,
                        AppliedBenefitsSnapshot = appliedBenefitsSnapshot,
                        IdempotencyKey = idempotencyKey,
                        OfflineTransactionId = offlineTransactionId,
                        OfflineReplayBatchCorrelationId = offlineReplayBatchCorrelationId
                    };

                    // TSE imzası oluştur (eğer gerekliyse). External call; if it fails we rollback the transaction (no DB changes committed yet).
                    if (effectiveTseRequired)
                    {
                        try
                        {
                            var sigResult = await FiscalTseSigning.SignAsync(
                                _tseService,
                                new FiscalSigningRequest(
                                    cashRegisterId,
                                    preReceiptNumber,
                                    payment.TotalAmount,
                                    registerNumber,
                                    TaxDetailsJson: JsonSerializer.Serialize(taxDetails),
                                    DbTransaction: transaction));
                            payment.TseSignature = sigResult.CompactJws;
                            payment.PrevSignatureValueUsed = sigResult.PrevSignatureValueUsed;
                            _logger.LogInformation("TSE signature generated for payment {PaymentId}", payment.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to generate TSE signature for payment {PaymentId}", payment.Id);
                            await transaction.RollbackAsync();
                            _context.ChangeTracker.Clear();
                            return new PaymentResult
                            {
                                Success = false,
                                Message = "Failed to generate TSE signature",
                                Errors = { "TSE signature generation failed" }
                            };
                        }
                    }

                    var companyAddress = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}";

                    posInvoice = new Invoice
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
                        CompanyName = _companyProfile.CompanyName,
                        CompanyTaxNumber = _companyProfile.TaxNumber,
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

                    _context.PaymentDetails.Add(payment);
                    _context.Invoices.Add(posInvoice);
                    await _receiptService.AddReceiptFromPaymentToContextAsync(payment);
                    // Stock updates: product entities were modified in the loop (tracked); SaveChanges persists them with payment/invoice/receipt in one commit.

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    committedPayment = payment!;
                    committedInvoice = posInvoice!;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    _logger.LogWarning(ex, "Daily allowance concurrency conflict for customer {CustomerId} during payment creation", request.CustomerId);
                    return ToDailyAllowanceConflictResult();
                }
                catch (DbUpdateException ex) when (IsIdempotencyKeyViolation(ex))
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    if (idempotencyKey != null)
                    {
                        var existing = await _context.PaymentDetails.AsNoTracking()
                            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);
                        if (existing != null)
                        {
                            var invoiceExists = await _context.Invoices.AsNoTracking().AnyAsync(i => i.SourcePaymentId == existing.Id);
                            _logger.LogInformation("Idempotency key race: returning existing payment {PaymentId} for key {Key}", existing.Id, idempotencyKey);
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
                                InvoicePersisted = invoiceExists
                            };
                        }
                    }
                    _logger.LogError(ex, "Fiscal transaction failed (idempotency key violation) for payment");
                    throw;
                }
                catch (DbUpdateException ex) when (IsBenefitDailyUsageConflict(ex))
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    _logger.LogWarning(ex, "Daily allowance unique constraint race for customer {CustomerId} during payment creation", request.CustomerId);
                    return ToDailyAllowanceConflictResult();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    _logger.LogError(ex, "Fiscal transaction failed for payment; Payment, Invoice, Receipt, and stock updates rolled back");
                    throw;
                }

                if (committedPayment != null && committedInvoice != null)
                {
                    var createdPayment = committedPayment;
                    var createdInvoice = committedInvoice;
                    _logger.LogInformation("Payment created successfully: {PaymentId} for customer {CustomerId} (Invoice {InvoiceId}, Receipt in same transaction)",
                        createdPayment.Id, customer.Id, createdInvoice.Id);

                    // Audit: append-only, after successful commit. Best-effort; failure does not roll back payment.
                    var paymentAuditCorrelation = offlineReplayBatchCorrelationId?.ToString("N");
                    await LogPaymentAuditAsync("PaymentCreated", "Payment", createdPayment.Id, userId,
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
                        await LogPaymentAuditAsync("ReceiptPersisted", "Receipt", persistedReceipt.ReceiptId, userId,
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

                    // FinanzOnline: best-effort after commit; failure does not roll back DB. Persist reconciliation state.
                    if (effectiveTseRequired)
                    {
                        try
                        {
                            _finanzOnlineMetrics?.IncrementSubmitTotal();
                            var foResult = await _finanzOnlineService.SubmitInvoiceAsync(createdInvoice).ConfigureAwait(false);
                            await UpdatePaymentFinanzOnlineStateAsync(createdPayment.Id, foResult, isRetry: false,
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
                            var kind = ClassifyFinanzOnlineFailure(ex);
                            _finanzOnlineMetrics?.IncrementSubmitFailed(kind);
                            _logger.LogWarning(ex, "Failed to send invoice to FinanzOnline: {InvoiceId}", createdInvoice.Id);
                            await UpdatePaymentFinanzOnlineStateAsync(createdPayment.Id, new FinanzOnlineSubmitResponse
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
                        InvoicePersisted = true
                    };
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
        /// Read-only eligibility preview: which benefits would apply for this customer and cart. No persistence (no BenefitDailyUsage write, no payment).
        /// </summary>
        public async Task<BenefitEligibilityPreviewResponse?> ComputeBenefitEligibilityPreviewAsync(BenefitEligibilityPreviewRequest request)
        {
            if (request == null || request.CustomerId == Guid.Empty)
                return null;

            var customer = await _customerRepository.GetByIdAsync(request.CustomerId);
            if (customer == null || !customer.IsActive)
                return null;

            var paymentItems = new List<PaymentItem>();
            var productIdToCategoryId = new Dictionary<Guid, Guid>();
            decimal totalAmount = 0;

            foreach (var item in request.Items ?? new List<BenefitEligibilityPreviewItemRequest>())
            {
                if (item.Quantity < 1) continue;
                var product = await _context.Products
                    .Include(p => p.CategoryNavigation)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                if (product?.CategoryNavigation == null) continue;

                var vatRatePercent = product.CategoryNavigation.VatRate;
                var line = CartMoneyHelper.ComputeLine(product.Price, item.Quantity, vatRatePercent);
                totalAmount += line.LineGross;
                paymentItems.Add(new PaymentItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = line.UnitPriceGross,
                    TotalPrice = line.LineGross,
                    TaxType = line.TaxType,
                    TaxRate = line.TaxRate,
                    TaxAmount = line.LineTax,
                    LineNet = line.LineNet
                });
                productIdToCategoryId[product.Id] = product.CategoryId;
            }

            var subtotalBeforeBenefits = totalAmount;
            var applicableList = new List<ApplicableBenefitPreviewDto>();
            var blockedList = new List<BlockedBenefitPreviewDto>();
            decimal totalDiscount = 0;
            var now = DateTime.UtcNow;

            // Percentage discount: single highest-priority assignment or customer fallback
            decimal effectivePct = 0;
            var assignedPercentage = await _context.BenefitAssignments
                .AsNoTracking()
                .Where(ba => ba.CustomerId == customer.Id && ba.IsActive
                    && ba.ValidFrom <= now && (ba.ValidTo == null || ba.ValidTo >= now))
                .Include(ba => ba.BenefitDefinition)
                .Where(ba => ba.BenefitDefinition.BenefitKind == AppliedBenefitKind.PercentageDiscount
                    && ba.BenefitDefinition.IsActive
                    && ba.BenefitDefinition.PercentageValue.HasValue
                    && ba.BenefitDefinition.PercentageValue > 0)
                .OrderByDescending(ba => ba.Priority)
                .ThenByDescending(ba => ba.BenefitDefinition.PercentageValue)
                .Select(ba => new { ba.BenefitDefinition.PercentageValue, ba.BenefitDefinition.Code })
                .FirstOrDefaultAsync();
            if (assignedPercentage != null && assignedPercentage.PercentageValue.HasValue && assignedPercentage.PercentageValue.Value > 0)
                effectivePct = assignedPercentage.PercentageValue.Value;
            else
                effectivePct = customer.DiscountPercentage;

            if (totalAmount > 0 && effectivePct > 0)
            {
                effectivePct = Math.Clamp(effectivePct, 0, 100);
                var discountAmount = CartMoneyHelper.Round(totalAmount * effectivePct / 100m);
                if (discountAmount > 0)
                {
                    totalAmount -= discountAmount;
                    totalDiscount += discountAmount;
                    applicableList.Add(new ApplicableBenefitPreviewDto
                    {
                        Kind = AppliedBenefitKind.PercentageDiscount,
                        Description = $"Customer discount {effectivePct}%",
                        Amount = -discountAmount,
                        Quantity = null,
                        BenefitDefinitionCode = assignedPercentage?.Code
                    });
                }
            }

            // FreeAllowance: read existing usage only (no insert/update)
            var todayUtc = now.Date;
            var freeAllowanceAssignments = await _context.BenefitAssignments
                .AsNoTracking()
                .Where(ba => ba.CustomerId == customer.Id && ba.IsActive
                    && ba.ValidFrom <= now && (ba.ValidTo == null || ba.ValidTo >= now))
                .Include(ba => ba.BenefitDefinition)
                .Where(ba => ba.BenefitDefinition.BenefitKind == AppliedBenefitKind.FreeAllowance
                    && ba.BenefitDefinition.IsActive
                    && ba.BenefitDefinition.AllowanceQuantity.HasValue
                    && ba.BenefitDefinition.AllowanceQuantity > 0
                    && ba.BenefitDefinition.AllowanceCategoryId.HasValue
                    && (ba.BenefitDefinition.AllowanceScope == null || ba.BenefitDefinition.AllowanceScope.ToLower() == "per_day"))
                .OrderByDescending(ba => ba.Priority)
                .ToListAsync();

            foreach (var assignment in freeAllowanceAssignments)
            {
                var def = assignment.BenefitDefinition;
                var allowanceQty = def.AllowanceQuantity!.Value;
                var categoryId = def.AllowanceCategoryId!.Value;

                var usageRow = await _context.BenefitDailyUsages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.CustomerId == customer.Id && u.BenefitDefinitionId == def.Id && u.UsageDate == todayUtc);
                var quantityUsed = usageRow?.QuantityUsed ?? 0;
                var remaining = allowanceQty - quantityUsed;

                if (remaining <= 0)
                {
                    blockedList.Add(new BlockedBenefitPreviewDto
                    {
                        Kind = AppliedBenefitKind.FreeAllowance,
                        BenefitDefinitionCode = def.Code,
                        BlockedReasonCode = BenefitBlockedReasonCodes.DailyLimitReached,
                        Message = "Daily allowance limit reached"
                    });
                    continue;
                }

                var eligibleItems = paymentItems.Where(pi => productIdToCategoryId.GetValueOrDefault(pi.ProductId) == categoryId).ToList();
                var eligibleQuantity = eligibleItems.Sum(i => i.Quantity);
                if (eligibleQuantity <= 0)
                {
                    blockedList.Add(new BlockedBenefitPreviewDto
                    {
                        Kind = AppliedBenefitKind.FreeAllowance,
                        BenefitDefinitionCode = def.Code,
                        BlockedReasonCode = BenefitBlockedReasonCodes.NoEligibleItems,
                        Message = "No cart items in allowance category"
                    });
                    continue;
                }

                var claimed = Math.Min(remaining, eligibleQuantity);
                var remainingToClaim = claimed;
                decimal discountAmount = 0;
                foreach (var pi in eligibleItems)
                {
                    if (remainingToClaim <= 0) break;
                    var n = Math.Min(pi.Quantity, remainingToClaim);
                    discountAmount += pi.UnitPrice * n;
                    remainingToClaim -= n;
                }
                discountAmount = CartMoneyHelper.Round(discountAmount);
                if (discountAmount > 0)
                {
                    totalAmount -= discountAmount;
                    totalDiscount += discountAmount;
                    applicableList.Add(new ApplicableBenefitPreviewDto
                    {
                        Kind = AppliedBenefitKind.FreeAllowance,
                        Description = $"Free allowance ({claimed} items)",
                        Amount = -discountAmount,
                        Quantity = claimed,
                        BenefitDefinitionCode = def.Code
                    });
                }
            }

            // BuyXGetY: single highest-priority; fail-safe: if quantity not reached, report blocked with RequiredMoreQuantity
            var buyXGetYAssignment = await _context.BenefitAssignments
                .AsNoTracking()
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

                if (totalQuantity < buyX)
                {
                    blockedList.Add(new BlockedBenefitPreviewDto
                    {
                        Kind = AppliedBenefitKind.BuyXGetY,
                        BenefitDefinitionCode = def.Code,
                        BlockedReasonCode = BenefitBlockedReasonCodes.QuantityNotReached,
                        Message = $"Buy {buyX} get {getY} free: add {buyX - totalQuantity} more item(s) to qualify",
                        RequiredMoreQuantity = buyX - totalQuantity
                    });
                }
                else
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
                            totalAmount -= discountAmount;
                            totalDiscount += discountAmount;
                            applicableList.Add(new ApplicableBenefitPreviewDto
                            {
                                Kind = AppliedBenefitKind.BuyXGetY,
                                Description = $"Buy {buyX} get {getY} free ({freeQty} items)",
                                Amount = -discountAmount,
                                Quantity = (int)freeQty,
                                BenefitDefinitionCode = def.Code
                            });
                        }
                    }
                }
            }

            return new BenefitEligibilityPreviewResponse
            {
                SubtotalBeforeBenefits = subtotalBeforeBenefits,
                TotalDiscountAmount = -totalDiscount,
                SubtotalAfterBenefits = totalAmount,
                ApplicableBenefits = applicableList,
                BlockedBenefits = blockedList
            };
        }

        /// <summary>
        /// QR payload (RKSV belegdaten veya NON_FISCAL_DEMO) ve demo/fiscal flag'leri üretir.
        /// tseRequired=false: Açık NON_FISCAL marker ile UI yanlışlıkla fiskal sanmasın.
        /// </summary>
        private async Task<(string QrPayload, bool IsDemoFiscal, string TseProvider)> BuildQrPayloadAndFlagsAsync(PaymentDetails payment, bool tseRequired)
        {
            var isDemoFiscal = !tseRequired || _tseOptions.UseSoftTseWhenNoDevice;
            var tseProvider = tseRequired ? (_tseOptions.UseSoftTseWhenNoDevice ? "Demo" : "Device") : "None";
            var kassenId = await _context.CashRegisters.AsNoTracking()
                .Where(r => r.Id == payment.CashRegisterId)
                .Select(r => r.RegisterNumber)
                .FirstOrDefaultAsync() ?? "";
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
                var validMethods = new[] { "cash", "card", "voucher", "transfer", "banktransfer" };
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

                var (fromUtc, toExclusiveUtc) =
                    PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(startDate, endDate);

                var (items, totalCount) = await _paymentRepository.GetPagedAsync(
                    pageNumber,
                    pageSize,
                    p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusiveUtc && p.IsActive,
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
        /// Cancel payment via fiscal storno (reversal). Original payment is NEVER modified; a reversal record is created with TSE signature and credit note.
        /// Sprint 6: optional idempotencyKey — retries with same key return existing storno.
        /// </summary>
        public async Task<PaymentResult> CancelPaymentAsync(Guid paymentId, string reason, string userId, string? idempotencyKey = null)
        {
            try
            {
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
                        _logger.LogInformation("Idempotent cancel: returning existing storno {StornoId} for payment {PaymentId} key {Key}", existingStorno.Id, paymentId, key);
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

                // Already cancelled = a storno (reversal) already exists for this payment. Original payment is never modified.
                var alreadyHasStorno = await _context.PaymentDetails.AsNoTracking()
                    .AnyAsync(p => p.OriginalPaymentId == paymentId && p.IsStorno);
                if (alreadyHasStorno)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment is already cancelled",
                        Errors = { "A reversal (storno) already exists for this payment" }
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

                return await CreateStornoReversalAsync(payment, reason, userId, key);
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
        /// Creates a fiscal storno (reversal) record for the given payment. Original payment is never modified.
        /// Creates: storno PaymentDetails, credit note Invoice, Receipt, TSE signature; reverts stock.
        /// </summary>
        private async Task<PaymentResult> CreateStornoReversalAsync(PaymentDetails payment, string reason, string userId, string? cancelIdempotencyKey)
        {
            var paymentId = payment.Id;
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

            var stornoId = Guid.NewGuid();
            var companyAddress = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}";
            string stornoBelegNr = string.Empty;
            Guid stornoInvoiceId = Guid.Empty;

            await using var dbTx = await _context.Database.BeginTransactionAsync();
            try
            {
                stornoBelegNr = await _receiptSequenceService.AllocateNextBelegNrInTransactionAsync(dbTx, cashRegisterId, registerNumber, DateTime.UtcNow);

                var storno = new PaymentDetails
                {
                    Id = stornoId,
                    CustomerId = payment.CustomerId,
                    CustomerName = payment.CustomerName,
                    PaymentItems = JsonDocument.Parse(JsonSerializer.Serialize(stornoItems)),
                    TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(stornoTaxDetails)),
                    OriginalPaymentId = paymentId,
                    OriginalReceiptId = originalReceipt?.ReceiptId,
                    IsRefund = false,
                    IsStorno = true,
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
                    CompanyName = _companyProfile.CompanyName,
                    CompanyTaxNumber = _companyProfile.TaxNumber,
                    CompanyAddress = companyAddress,
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
                    StornoReasonCode = null,
                    StornoReasonText = reason,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    IsActive = true
                };

                _context.PaymentDetails.Add(storno);
                _context.Invoices.Add(stornoInvoice);
                await _receiptService.AddReceiptFromPaymentToContextAsync(storno);

                foreach (var item in originalItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                        product.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                await dbTx.CommitAsync();
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
                        _logger.LogInformation("Idempotent cancel (race): returning existing storno {StornoId} for key {Key}", existing.Id, cancelIdempotencyKey);
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
                    CreatedAt = stornoRow.CreatedAt
                });

            return new PaymentResult
            {
                Success = true,
                Message = "Payment cancelled successfully",
                Payment = stornoRow
            };
        }

        /// <summary>
        /// Ödeme iade et. Sprint 6: optional idempotencyKey — retries with same key return existing refund (no duplicate BelegNr/stock).
        /// </summary>
        public async Task<PaymentResult> RefundPaymentAsync(Guid paymentId, decimal amount, string reason, string userId, string? idempotencyKey = null)
        {
            try
            {
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

                decimal refundRatio = amount / payment.TotalAmount;
                decimal refundTaxAmount = -payment.TaxAmount * refundRatio;

                // Sprint 6: Idempotency — if key provided and we already have a refund for this payment with this key, return it.
                var refundKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
                if (refundKey != null)
                {
                    var existingRefund = await _context.PaymentDetails.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdempotencyKey == refundKey && p.IsRefund && p.OriginalPaymentId == paymentId);
                    if (existingRefund != null)
                    {
                        _logger.LogInformation("Idempotent refund: returning existing refund {RefundId} for payment {PaymentId} key {Key}", existingRefund.Id, paymentId, refundKey);
                        return new PaymentResult
                        {
                            Success = true,
                            Message = "Refund processed successfully",
                            Payment = existingRefund
                        };
                    }
                }

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

                var refundId = Guid.NewGuid();
                var companyAddress = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}";
                string refundBelegNr = string.Empty;
                Guid refundInvoiceId = Guid.Empty;

                await using var refundTx = await _context.Database.BeginTransactionAsync();
                try
                {
                    refundBelegNr = await _receiptSequenceService.AllocateNextBelegNrInTransactionAsync(refundTx, refundCashRegisterId, refundRegisterNumber, DateTime.UtcNow);

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
                        RefundReason = reason,
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
                        CompanyName = _companyProfile.CompanyName,
                        CompanyTaxNumber = _companyProfile.TaxNumber,
                        CompanyAddress = companyAddress,
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

                    _context.PaymentDetails.Add(refund);
                    _context.Invoices.Add(refundInvoice);
                    await _receiptService.AddReceiptFromPaymentToContextAsync(refund);

                    foreach (var item in originalItems)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            var refundQuantity = (int)Math.Round(item.Quantity * refundRatio);
                            if (refundQuantity > 0)
                            {
                                product.StockQuantity += refundQuantity;
                                product.UpdatedAt = DateTime.UtcNow;
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
                            .FirstOrDefaultAsync(p => p.IdempotencyKey == refundKey && p.IsRefund);
                        if (existing != null)
                        {
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

            var (fromUtc, toExclusiveUtc) =
                PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(startDate, endDate);

            // Query using PaymentMethodRaw - no InvalidCastException since it's varchar
            var payments = await _context.PaymentDetails
                .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusiveUtc && p.IsActive)
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

                if (payment.CashRegisterId == Guid.Empty)
                    throw new InvalidOperationException("Payment has no CashRegisterId; cannot sign.");
                var regForSign = await _context.CashRegisters.FirstOrDefaultAsync(r => r.Id == payment.CashRegisterId)
                    ?? throw new InvalidOperationException($"Cash register {payment.CashRegisterId} not found.");

                var sigResult = await FiscalTseSigning.SignAsync(
                    _tseService,
                    new FiscalSigningRequest(
                        payment.CashRegisterId,
                        payment.ReceiptNumber ?? payment.Id.ToString(),
                        payment.TotalAmount,
                        regForSign.RegisterNumber,
                        TaxDetailsJson: payment.TaxDetails?.RootElement.GetRawText()));
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
                    CompanyName = _companyProfile.CompanyName,
                    CompanyTaxNumber = _companyProfile.TaxNumber,
                    CompanyAddress = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}",
                    TseSignature = payment.TseSignature,
                    KassenId = (await _context.CashRegisters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == payment.CashRegisterId))?.RegisterNumber
                        ?? throw new InvalidOperationException("Cash register not found for FinanzOnline submit."),
                    TseTimestamp = payment.CreatedAt,
                    CashRegisterId = payment.CashRegisterId,
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

        #region Private Methods

        /// <summary>Resolves user role for audit. Returns "Unknown" if user not found.</summary>
        private async Task<string> GetUserRoleAsync(string userId)
        {
            var user = await _userService.GetUserByIdAsync(userId);
            return user?.Role ?? "Unknown";
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

        /// <summary>Sprint 6: True when the exception is a unique constraint violation on cancel_idempotency_key.</summary>
        private static bool IsCancelIdempotencyKeyViolation(DbUpdateException ex)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e is PostgresException pg && pg.SqlState == "23505")
                {
                    var name = pg.ConstraintName ?? "";
                    if (name.Contains("cancel", StringComparison.OrdinalIgnoreCase) && name.Contains("idempotency", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
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

        /// <summary>Persist FinanzOnline reconciliation state on PaymentDetails after submit (post-commit). Best-effort; does not throw. Logs FO attempt to audit for incident investigation (correlation id + retry history).</summary>
        private async Task UpdatePaymentFinanzOnlineStateAsync(Guid paymentId, FinanzOnlineSubmitResponse result, bool isRetry,
            string? userIdForAudit = null,
            string? correlationIdForAudit = null)
        {
            try
            {
                var payment = await _context.PaymentDetails.FindAsync(paymentId).ConfigureAwait(false);
                if (payment == null) return;

                payment.FinanzOnlineLastAttemptAtUtc = result.SubmittedAt;
                payment.FinanzOnlineError = result.Success ? null : TruncateForDb(result.ErrorMessage, 500);
                payment.FinanzOnlineReferenceId = result.ReferenceId;
                if (isRetry) payment.FinanzOnlineRetryCount++;

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

        private static string? TruncateForDb(string? value, int maxLen)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return value.Length <= maxLen ? value : value.Substring(0, maxLen - 3) + "...";
        }

        private static FinanzOnlineFailureKind ClassifyFinanzOnlineFailure(Exception ex)
        {
            if (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
                return FinanzOnlineFailureKind.Transient;
            var msg = (ex.Message ?? "").ToLowerInvariant();
            if (msg.Contains("duplicate") || msg.Contains("already submitted") || msg.Contains("validation") || msg.Contains("forbidden"))
                return FinanzOnlineFailureKind.Permanent;
            return FinanzOnlineFailureKind.Unknown;
        }

        #endregion
    }
}
