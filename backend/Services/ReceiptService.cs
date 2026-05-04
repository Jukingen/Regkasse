using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services
{
    public interface IReceiptService
    {
        Task<ReceiptDTO?> GetReceiptAsync(Guid receiptId);
        /// <summary>Returns persisted receipt by payment id. No lazy generation; receipt must have been created at payment time.</summary>
        Task<ReceiptDTO?> GetReceiptByPaymentIdAsync(Guid paymentId);
        Task<ReceiptDTO> CreateReceiptFromPaymentAsync(Guid paymentId);
        /// <summary>Sprint 2: Builds Receipt + Items + TaxLines from payment and adds to context without saving. Caller must SaveChanges. Receipt includes totals, tax breakdown, signature, QR payload.</summary>
        Task AddReceiptFromPaymentToContextAsync(PaymentDetails payment);
        Task<PagedResult<ReceiptListItemDto>> GetReceiptListAsync(int page, int pageSize, string? sort, string? receiptNumber, string? cashRegisterId, string? cashierId, DateTime? issuedFrom, DateTime? issuedTo);
    }

    public class ReceiptService : IReceiptService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReceiptService> _logger;
        private readonly ITseService _tseService;
        private readonly CompanyProfileOptions _companyProfile;
        private readonly IUserService _userService;
        private readonly ISettingsTenantResolver _settingsTenantResolver;

        public ReceiptService(
            AppDbContext context,
            ILogger<ReceiptService> logger,
            ITseService tseService,
            Microsoft.Extensions.Options.IOptions<CompanyProfileOptions> companyProfile,
            IUserService userService,
            ISettingsTenantResolver settingsTenantResolver)
        {
            _context = context;
            _logger = logger;
            _tseService = tseService;
            _companyProfile = companyProfile.Value;
            _userService = userService;
            _settingsTenantResolver = settingsTenantResolver;
        }

        /// <summary>Returns persisted receipt by ReceiptId or PaymentId. No lazy generation.</summary>
        public async Task<ReceiptDTO?> GetReceiptAsync(Guid receiptId)
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
            var receipt = await _context.Receipts
                .Include(r => r.Items)
                .Include(r => r.TaxLines)
                .Include(r => r.Payment)!.ThenInclude(p => p!.OfflineTransaction)
                .Where(r => (r.ReceiptId == receiptId || r.PaymentId == receiptId)
                    && _context.CashRegisters.Any(cr => cr.Id == r.CashRegisterId && cr.TenantId == tenantId))
                .FirstOrDefaultAsync();

            return receipt != null ? await MapToDtoAsync(receipt) : null;
        }

        /// <inheritdoc />
        public async Task<ReceiptDTO?> GetReceiptByPaymentIdAsync(Guid paymentId)
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
            var receipt = await _context.Receipts
                .Include(r => r.Items)
                .Include(r => r.TaxLines)
                .Include(r => r.Payment)
                .Where(r => r.PaymentId == paymentId
                    && _context.CashRegisters.Any(cr => cr.Id == r.CashRegisterId && cr.TenantId == tenantId))
                .FirstOrDefaultAsync();

            return receipt != null ? await MapToDtoAsync(receipt) : null;
        }

        public async Task<ReceiptDTO> CreateReceiptFromPaymentAsync(Guid paymentId)
        {
            // 1. Check if receipt already exists
            var effectiveTenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
            var existingReceipt = await _context.Receipts
                .Include(r => r.Items)
                .Include(r => r.TaxLines)
                .Include(r => r.Payment)
                .Where(r => r.PaymentId == paymentId
                    && _context.CashRegisters.Any(cr => cr.Id == r.CashRegisterId && cr.TenantId == effectiveTenantId))
                .FirstOrDefaultAsync();

            if (existingReceipt != null)
            {
                return await MapToDtoAsync(existingReceipt);
            }

            // 2. Fetch payment (using repository or context)
            var payment = await _context.PaymentDetails.FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null) throw new KeyNotFoundException($"Payment {paymentId} not found");

            var paymentInTenant = await _context.CashRegisters.AsNoTracking()
                .AnyAsync(cr => cr.Id == payment.CashRegisterId && cr.TenantId == effectiveTenantId);
            if (!paymentInTenant) throw new KeyNotFoundException($"Payment {paymentId} not found");

            // 3. Parse items
            var items = new List<PaymentItem>();
            try
            {
                if (payment.PaymentItems != null && payment.PaymentItems.RootElement.ValueKind != JsonValueKind.Undefined)
                {
                    items = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.RootElement.GetRawText()) ?? new List<PaymentItem>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize items for payment {PaymentId}", paymentId);
            }

            // 4. Signature: Payment.TseSignature = COMPACT JWS. PrevSignatureValue = imza zinciri (stateful)
            var registerNumber = await ResolveRegisterNumberAsync(payment.CashRegisterId);
            var signatureValue = payment.TseSignature ?? string.Empty;
            var prevSignatureValue = payment.PrevSignatureValueUsed
                ?? await GetLastSignatureValueForCashRegisterAsync(payment.CashRegisterId);

            var certInfo = await _tseService.GetTseCertificateInfoAsync(registerNumber);
            var qrPayload = string.IsNullOrEmpty(signatureValue) ? string.Empty : $"_R1-AT1_{registerNumber}_{payment.ReceiptNumber}_{payment.CreatedAt:s}_{payment.TotalAmount:0.00}_0.00_{certInfo.CertificateNumber}_{signatureValue}";

            // 5. Create Entity
            var newReceipt = new Receipt
            {
                ReceiptId = Guid.NewGuid(),
                PaymentId = payment.Id,
                ReceiptNumber = payment.ReceiptNumber ?? $"TEMP-{payment.Id.ToString()[..8]}",
                IssuedAt = payment.CreatedAt,
                CashierId = payment.CashierId,
                CashRegisterId = payment.CashRegisterId,
                SubTotal = payment.TotalAmount - payment.TaxAmount,
                TaxTotal = payment.TaxAmount,
                GrandTotal = payment.TotalAmount,
                QrCodePayload = qrPayload,
                SignatureValue = signatureValue,
                PrevSignatureValue = prevSignatureValue,
                CreatedAt = DateTime.UtcNow
            };

            // 6. Items: Phase 2 flat-first. Product-only (no Modifiers) → one ReceiptItem with full totals. Legacy (Modifiers present) → main line (base only) + nested modifier lines.
            var receiptItems = new List<ReceiptItem>();
            var taxLineInputs = new List<(int TaxType, decimal TaxRate, decimal LineNet, decimal LineTax, decimal LineGross)>();
            var legacySnapshotItemCount = 0;
            foreach (var i in items)
            {
                var productItemId = Guid.NewGuid();
                var hasLegacyModifiers = i.Modifiers != null && i.Modifiers.Count > 0;
                if (hasLegacyModifiers)
                    legacySnapshotItemCount++;
                var modifierNet = i.Modifiers?.Sum(m => m.LineNet) ?? 0;
                var modifierTax = i.Modifiers?.Sum(m => m.TaxAmount) ?? 0;
                var modifierGross = i.Modifiers?.Sum(m => m.TotalPrice) ?? 0;
                receiptItems.Add(new ReceiptItem
                {
                    ItemId = productItemId,
                    ReceiptId = newReceipt.ReceiptId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = hasLegacyModifiers ? i.TotalPrice - modifierGross : i.TotalPrice,
                    LineNet = hasLegacyModifiers ? i.LineNet - modifierNet : i.LineNet,
                    VatAmount = hasLegacyModifiers ? i.TaxAmount - modifierTax : i.TaxAmount,
                    TaxRate = i.TaxRate * 100, // 0.20 -> 20.00
                    ParentItemId = null,
                    CategoryName = null
                });
                taxLineInputs.Add((i.TaxType, i.TaxRate, hasLegacyModifiers ? i.LineNet - modifierNet : i.LineNet, hasLegacyModifiers ? i.TaxAmount - modifierTax : i.TaxAmount, hasLegacyModifiers ? i.TotalPrice - modifierGross : i.TotalPrice));
                if (hasLegacyModifiers)
                {
                    foreach (var m in i.Modifiers!)
                    {
                        receiptItems.Add(new ReceiptItem
                        {
                            ItemId = Guid.NewGuid(),
                            ReceiptId = newReceipt.ReceiptId,
                            ProductName = "+ " + m.Name,
                            Quantity = i.Quantity,
                            UnitPrice = m.UnitPrice,
                            TotalPrice = m.TotalPrice,
                            LineNet = m.LineNet,
                            VatAmount = m.TaxAmount,
                            TaxRate = m.TaxRate * 100,
                            ParentItemId = productItemId,
                            CategoryName = null
                        });
                        taxLineInputs.Add((m.TaxType, m.TaxRate, m.LineNet, m.TaxAmount, m.TotalPrice));
                    }
                }
            }
            newReceipt.Items = receiptItems;

            // Phase 2 observability: when this log stops appearing, no receipts are created from PaymentItem.Modifiers (legacy snapshots) anymore.
            if (legacySnapshotItemCount > 0)
                _logger.LogInformation("Phase2.LegacyModifier.ReceiptCreatedFromLegacyModifierSnapshots PaymentId={PaymentId} ReceiptId={ReceiptId} ItemsWithLegacyModifiersCount={ItemsWithLegacyModifiersCount}", paymentId, newReceipt.ReceiptId, legacySnapshotItemCount);

            // 7. Totals: satırlardan topla (deterministik; aynı input => aynı output)
            newReceipt.SubTotal = receiptItems.Sum(x => x.LineNet);
            newReceipt.TaxTotal = receiptItems.Sum(x => x.VatAmount);
            newReceipt.GrandTotal = receiptItems.Sum(x => x.TotalPrice);

            // 8. Tax Lines: tüm satırlardan (ürün + modifier) vergi grubu; RKSV uyumu
            var taxGroups = taxLineInputs
                .GroupBy(x => new { x.TaxType, x.TaxRate })
                .Select(g => new ReceiptTaxLine
                {
                    LineId = Guid.NewGuid(),
                    TaxType = g.Key.TaxType,
                    TaxRate = g.Key.TaxRate * 100,
                    NetAmount = g.Sum(x => x.LineNet),
                    TaxAmount = g.Sum(x => x.LineTax),
                    GrossAmount = g.Sum(x => x.LineGross)
                })
                .OrderBy(t => t.TaxRate)
                .ThenBy(t => t.TaxType)
                .ToList();

            newReceipt.TaxLines = taxGroups;

            // 9. Save
            _context.Receipts.Add(newReceipt);
            await _context.SaveChangesAsync();

            // Reload to get Payment navigation populated (or just set it manually for DTO mapping)
            newReceipt.Payment = payment;

            var signatureDto = new ReceiptSignatureDTO
            {
                Algorithm = "ES256",
                SerialNumber = certInfo.CertificateNumber,
                Timestamp = payment.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                PrevSignatureValue = prevSignatureValue,
                SignatureValue = signatureValue,
                QrData = qrPayload
            };
            return await MapToDtoAsync(newReceipt, signatureDto);
        }

        /// <inheritdoc />
        public async Task AddReceiptFromPaymentToContextAsync(PaymentDetails payment)
        {
            var items = new List<PaymentItem>();
            try
            {
                if (payment.PaymentItems != null && payment.PaymentItems.RootElement.ValueKind != JsonValueKind.Undefined)
                    items = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.RootElement.GetRawText()) ?? new List<PaymentItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize items for payment {PaymentId}", payment.Id);
            }

            var registerNumberCtx = await ResolveRegisterNumberAsync(payment.CashRegisterId);
            var signatureValue = payment.TseSignature ?? string.Empty;
            var prevSignatureValue = payment.PrevSignatureValueUsed ?? await GetLastSignatureValueForCashRegisterAsync(payment.CashRegisterId);
            var certInfo = await _tseService.GetTseCertificateInfoAsync(registerNumberCtx);
            var qrPayload = string.IsNullOrEmpty(signatureValue) ? string.Empty : $"_R1-AT1_{registerNumberCtx}_{payment.ReceiptNumber}_{payment.CreatedAt:s}_{payment.TotalAmount:0.00}_0.00_{certInfo.CertificateNumber}_{signatureValue}";

            var newReceipt = new Receipt
            {
                ReceiptId = Guid.NewGuid(),
                PaymentId = payment.Id,
                ReceiptNumber = payment.ReceiptNumber ?? $"TEMP-{payment.Id.ToString()[..8]}",
                IssuedAt = payment.CreatedAt,
                CashierId = payment.CashierId,
                CashRegisterId = payment.CashRegisterId,
                SubTotal = payment.TotalAmount - payment.TaxAmount,
                TaxTotal = payment.TaxAmount,
                GrandTotal = payment.TotalAmount,
                QrCodePayload = qrPayload,
                SignatureValue = signatureValue,
                PrevSignatureValue = prevSignatureValue,
                CreatedAt = DateTime.UtcNow
            };

            var receiptItems = new List<ReceiptItem>();
            var taxLineInputs = new List<(int TaxType, decimal TaxRate, decimal LineNet, decimal LineTax, decimal LineGross)>();
            foreach (var i in items)
            {
                var productItemId = Guid.NewGuid();
                var hasLegacyModifiers = i.Modifiers != null && i.Modifiers.Count > 0;
                var modifierNet = i.Modifiers?.Sum(m => m.LineNet) ?? 0;
                var modifierTax = i.Modifiers?.Sum(m => m.TaxAmount) ?? 0;
                var modifierGross = i.Modifiers?.Sum(m => m.TotalPrice) ?? 0;
                receiptItems.Add(new ReceiptItem
                {
                    ItemId = productItemId,
                    ReceiptId = newReceipt.ReceiptId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = hasLegacyModifiers ? i.TotalPrice - modifierGross : i.TotalPrice,
                    LineNet = hasLegacyModifiers ? i.LineNet - modifierNet : i.LineNet,
                    VatAmount = hasLegacyModifiers ? i.TaxAmount - modifierTax : i.TaxAmount,
                    TaxRate = i.TaxRate * 100,
                    ParentItemId = null,
                    CategoryName = null
                });
                taxLineInputs.Add((i.TaxType, i.TaxRate, hasLegacyModifiers ? i.LineNet - modifierNet : i.LineNet, hasLegacyModifiers ? i.TaxAmount - modifierTax : i.TaxAmount, hasLegacyModifiers ? i.TotalPrice - modifierGross : i.TotalPrice));
                if (hasLegacyModifiers)
                {
                    foreach (var m in i.Modifiers!)
                    {
                        receiptItems.Add(new ReceiptItem
                        {
                            ItemId = Guid.NewGuid(),
                            ReceiptId = newReceipt.ReceiptId,
                            ProductName = "+ " + m.Name,
                            Quantity = i.Quantity,
                            UnitPrice = m.UnitPrice,
                            TotalPrice = m.TotalPrice,
                            LineNet = m.LineNet,
                            VatAmount = m.TaxAmount,
                            TaxRate = m.TaxRate * 100,
                            ParentItemId = productItemId,
                            CategoryName = null
                        });
                        taxLineInputs.Add((m.TaxType, m.TaxRate, m.LineNet, m.TaxAmount, m.TotalPrice));
                    }
                }
            }
            newReceipt.Items = receiptItems;
            newReceipt.SubTotal = receiptItems.Sum(x => x.LineNet);
            newReceipt.TaxTotal = receiptItems.Sum(x => x.VatAmount);
            newReceipt.GrandTotal = receiptItems.Sum(x => x.TotalPrice);

            var taxGroups = taxLineInputs
                .GroupBy(x => new { x.TaxType, x.TaxRate })
                .Select(g => new ReceiptTaxLine
                {
                    LineId = Guid.NewGuid(),
                    TaxType = g.Key.TaxType,
                    TaxRate = g.Key.TaxRate * 100,
                    NetAmount = g.Sum(x => x.LineNet),
                    TaxAmount = g.Sum(x => x.LineTax),
                    GrossAmount = g.Sum(x => x.LineGross)
                })
                .OrderBy(t => t.TaxRate)
                .ThenBy(t => t.TaxType)
                .ToList();
            newReceipt.TaxLines = taxGroups;

            _context.Receipts.Add(newReceipt);
        }

        public async Task<PagedResult<ReceiptListItemDto>> GetReceiptListAsync(int page, int pageSize, string? sort, string? receiptNumber, string? cashRegisterId, string? cashierId, DateTime? issuedFrom, DateTime? issuedTo)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 100) pageSize = 100;

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
            var queryable = _context.Receipts.AsNoTracking()
                .Include(r => r.Payment)
                .Where(r => _context.CashRegisters.Any(cr => cr.Id == r.CashRegisterId && cr.TenantId == tenantId));

            if (!string.IsNullOrWhiteSpace(receiptNumber))
                queryable = queryable.Where(r => r.ReceiptNumber != null && EF.Functions.ILike(r.ReceiptNumber, $"%{receiptNumber.Trim()}%"));
            if (!string.IsNullOrWhiteSpace(cashRegisterId))
            {
                if (Guid.TryParse(cashRegisterId.Trim(), out var crId))
                    queryable = queryable.Where(r => r.CashRegisterId == crId);
            }
            if (!string.IsNullOrWhiteSpace(cashierId))
                queryable = queryable.Where(r => r.CashierId != null && EF.Functions.ILike(r.CashierId, $"%{cashierId.Trim()}%"));
            if (issuedFrom.HasValue)
            {
                var fromDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(issuedFrom.Value);
                var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(fromDay);
                queryable = queryable.Where(r => r.IssuedAt >= fromUtc);
            }
            if (issuedTo.HasValue)
            {
                var toDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(issuedTo.Value);
                var (_, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(toDay);
                queryable = queryable.Where(r => r.IssuedAt < toExclusiveUtc);
            }

            var totalCount = await queryable.CountAsync();

            var sortField = "issuedAt";
            var sortDir = "desc";
            if (!string.IsNullOrWhiteSpace(sort))
            {
                var parts = sort.Trim().Split(':');
                if (parts.Length >= 1 && !string.IsNullOrEmpty(parts[0])) sortField = parts[0].ToLowerInvariant();
                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1])) sortDir = parts[1].ToLowerInvariant();
            }

            var isAsc = sortDir == "asc";
            queryable = sortField switch
            {
                "grandtotal" => isAsc ? queryable.OrderBy(r => r.GrandTotal) : queryable.OrderByDescending(r => r.GrandTotal),
                "receiptnumber" => isAsc ? queryable.OrderBy(r => r.ReceiptNumber) : queryable.OrderByDescending(r => r.ReceiptNumber),
                "createdat" => isAsc ? queryable.OrderBy(r => r.CreatedAt) : queryable.OrderByDescending(r => r.CreatedAt),
                _ => isAsc ? queryable.OrderBy(r => r.IssuedAt) : queryable.OrderByDescending(r => r.IssuedAt)
            };

            var items = await queryable
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReceiptListItemDto
                {
                    ReceiptId = r.ReceiptId,
                    PaymentId = r.PaymentId,
                    ReceiptNumber = r.ReceiptNumber,
                    IssuedAt = r.IssuedAt,
                    CashierId = r.CashierId,
                    CashRegisterEntityId = r.CashRegisterId,
                    CashRegisterId = r.CashRegisterId.ToString(),
                    RegisterDisplayNumber = _context.CashRegisters.Where(cr => cr.Id == r.CashRegisterId).Select(cr => cr.RegisterNumber).FirstOrDefault() ?? string.Empty,
                    SubTotal = r.SubTotal,
                    TaxTotal = r.TaxTotal,
                    GrandTotal = r.GrandTotal,
                    CreatedAt = r.CreatedAt,
                    RksvSpecialReceiptKind = r.Payment != null ? r.Payment.RksvSpecialReceiptKind : null,
                    RksvFinanzOnlineSubmissionStatus = _context.RksvSpecialReceiptFinanzOnlineSubmissions
                        .Where(s => s.PaymentId == r.PaymentId)
                        .Select(s => s.Status)
                        .FirstOrDefault(),
                })
                .ToListAsync();

            return new PagedResult<ReceiptListItemDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        private async Task<string> ResolveRegisterNumberAsync(Guid cashRegisterId)
        {
            if (cashRegisterId == Guid.Empty)
                throw new InvalidOperationException("Payment has no CashRegisterId.");
            var n = await _context.CashRegisters.AsNoTracking()
                .Where(r => r.Id == cashRegisterId)
                .Select(r => r.RegisterNumber)
                .FirstOrDefaultAsync();
            if (string.IsNullOrEmpty(n))
                throw new InvalidOperationException($"Cash register {cashRegisterId} not found.");
            return n;
        }

        /// <summary>Last signature for chain display: signature_chain_state then latest receipt for same register.</summary>
        private async Task<string> GetLastSignatureValueForCashRegisterAsync(Guid cashRegisterId)
        {
            if (cashRegisterId == Guid.Empty) return string.Empty;
            var fromChain = await _context.SignatureChainState
                .AsNoTracking()
                .Where(s => s.CashRegisterId == cashRegisterId)
                .Select(s => s.LastSignature)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(fromChain))
                return fromChain;
            var last = await _context.Receipts
                .Include(r => r.Payment)
                .Where(r => r.Payment != null && r.Payment.CashRegisterId == cashRegisterId && r.SignatureValue != null)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();
            return last?.SignatureValue ?? string.Empty;
        }

        private async Task<ReceiptDTO> MapToDtoAsync(Receipt receipt, ReceiptSignatureDTO? explicitSig = null)
        {
            var cashierIdStr = receipt.CashierId ?? string.Empty;
            string? cashierDisplay = null;
            if (!string.IsNullOrEmpty(cashierIdStr))
            {
                var appUser = await _userService.GetUserByIdAsync(cashierIdStr).ConfigureAwait(false);
                var n = appUser?.Name?.Trim();
                if (!string.IsNullOrEmpty(n))
                    cashierDisplay = n;
            }

            var header = new ReceiptHeaderDTO
            {
                ShopName = _companyProfile.CompanyName,
                Address = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}"
            };

            var company = new ReceiptCompanyDTO
            {
                Name = _companyProfile.CompanyName,
                Address = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}",
                TaxNumber = receipt.Payment?.Steuernummer ?? _companyProfile.TaxNumber
            };

            var signature = explicitSig ?? new ReceiptSignatureDTO
            {
                Algorithm = "ES256",
                SerialNumber = "",
                Timestamp = receipt.IssuedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                PrevSignatureValue = receipt.PrevSignatureValue ?? "",
                SignatureValue = receipt.SignatureValue ?? "",
                QrData = receipt.QrCodePayload ?? ""
            };

            var pay = receipt.Payment;
            var traceKind = pay?.IsStorno == true ? "Storno" : pay?.IsRefund == true ? "Refund" : null;

            var registerDisplay = await _context.CashRegisters.AsNoTracking()
                .Where(reg => reg.Id == receipt.CashRegisterId)
                .Select(reg => reg.RegisterNumber)
                .FirstOrDefaultAsync().ConfigureAwait(false) ?? receipt.CashRegisterId.ToString();

            var off = pay?.OfflineTransaction;

            RksvFinanzOnlineSubmissionStatusDto? rksvFonSubmission = null;
            if (pay != null)
            {
                var k = pay.RksvSpecialReceiptKind;
                if (k == RksvSpecialReceiptKinds.Startbeleg || k == RksvSpecialReceiptKinds.Jahresbeleg)
                {
                    rksvFonSubmission = await _context.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking()
                        .Where(s => s.PaymentId == receipt.PaymentId)
                        .Select(s => new RksvFinanzOnlineSubmissionStatusDto
                        {
                            Status = s.Status,
                            LastAttemptAtUtc = s.LastAttemptAtUtc,
                            LastErrorCode = s.LastErrorCode,
                            LastErrorMessage = s.LastErrorMessage,
                            ExternalReference = s.ExternalReference,
                            AttemptCount = s.AttemptCount,
                            SubmittedAtUtc = s.SubmittedAtUtc,
                            VerifiedAtUtc = s.VerifiedAtUtc,
                        })
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);
                }
            }

            return new ReceiptDTO
            {
                ReceiptId = receipt.ReceiptId,
                PaymentId = receipt.PaymentId,
                CashRegisterId = receipt.CashRegisterId,
                OriginalPaymentId = pay?.OriginalPaymentId,
                OriginalSaleReceiptId = pay?.OriginalReceiptId,
                FiscalTraceKind = traceKind,
                RksvSpecialReceiptKind = pay?.RksvSpecialReceiptKind,
                RksvNullbelegActsAsJahresbeleg = pay?.RksvNullbelegActsAsJahresbeleg ?? false,
                RksvFinanzOnlineSubmission = rksvFonSubmission,
                HasOfflineOrigin = pay?.OfflineTransactionId != null,
                OfflineTransactionId = pay?.OfflineTransactionId,
                OfflineCreatedAtUtc = off?.OfflineCreatedAtUtc,
                FiscalizedAtUtc = off?.FiscalizedAtUtc,
                ClockDriftWarning = off?.ClockDriftWarning ?? false,
                SequenceGapDetected = off?.SequenceGapDetected ?? false,
                SequenceDuplicateDetected = off?.SequenceDuplicateDetected ?? false,
                ReceiptNumber = receipt.ReceiptNumber,
                Date = receipt.IssuedAt,
                ReceiptPersistedAtUtc = receipt.CreatedAt,
                CashierId = cashierIdStr,
                CashierDisplayName = cashierDisplay,
                KassenID = registerDisplay,
                DisplayRegisterNumber = registerDisplay,
                TableNumber = receipt.Payment?.TableNumber,
                
                Company = company,
                Header = header,
                
                Items = receipt.Items.Select(i => new ReceiptItemDTO
                {
                    ItemId = i.ItemId,
                    Name = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    LineTotalNet = i.LineNet,
                    LineTotalGross = i.TotalPrice,
                    TaxRate = i.TaxRate,
                    VatRate = i.TaxRate / 100m,
                    VatAmount = i.VatAmount,
                    CategoryName = i.CategoryName,
                    ParentItemId = i.ParentItemId,
                    IsModifierLine = i.ParentItemId != null
                }).ToList(),
                
                SubTotal = receipt.SubTotal,
                TaxAmount = receipt.TaxTotal,
                GrandTotal = receipt.GrandTotal,
                Totals = new ReceiptTotalsDTO
                {
                    TotalNet = receipt.SubTotal,
                    TotalVat = receipt.TaxTotal,
                    TotalGross = receipt.GrandTotal
                },
                
                TaxRates = receipt.TaxLines.Select(t => new ReceiptTaxLineDTO
                {
                    TaxType = t.TaxType,
                    Rate = t.TaxRate,
                    VatRate = t.TaxRate / 100m,
                    GrossAmount = t.GrossAmount,
                    TaxAmount = t.TaxAmount,
                    NetAmount = t.NetAmount
                }).ToList(),
                
                Payments = new List<ReceiptPaymentDTO>
                {
                    new ReceiptPaymentDTO
                    {
                        Method = receipt.Payment?.PaymentMethod.ToString() ?? "Cash",
                        Amount = receipt.GrandTotal,
                        Tendered = receipt.GrandTotal,
                        Change = 0
                    }
                },
                
                Signature = signature,
                FooterText = _companyProfile.FooterText
            };
        }

    }
}
