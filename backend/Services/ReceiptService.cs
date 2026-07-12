using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using KasseAPI_Final.Services.Rksv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        /// <summary>
        /// RKSV receipt DTO from payment: persisted receipt when present, otherwise ephemeral build (no DB insert).
        /// Company header prefers <see cref="PaymentDetails"/> snapshot, then live <see cref="CompanySettings"/>.
        /// </summary>
        Task<ReceiptDTO> GenerateReceiptAsync(PaymentDetails payment);
        Task<PagedResult<ReceiptListItemDto>> GetReceiptListAsync(int page, int pageSize, string? sort, string? receiptNumber, string? cashRegisterId, string? cashierId, DateTime? issuedFrom, DateTime? issuedTo);
        /// <summary>RKSV receipt footer: shortened TSE compact JWS for thermal/display output.</summary>
        string GetTseSignatureDisplay(PaymentDetails payment);
        /// <summary>RKSV compliance label for receipt QR block (Development/Staging vs Production).</summary>
        string GetRksvFooter(IHostEnvironment env);
    }

    public class ReceiptService : IReceiptService
    {
        internal const string DemoRksvFooterLabel = "DEMO / NICHT FISKAL";
        internal const string ProductionRksvFooterLabel = "RKSV-konform";

        private readonly AppDbContext _context;
        private readonly ILogger<ReceiptService> _logger;
        private readonly ITseService _tseService;
        private readonly ICompanyProfileProvider _companyProfileProvider;
        private readonly IUserService _userService;
        private readonly ISettingsTenantResolver _settingsTenantResolver;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IRksvEnvironmentService _rksvEnvironment;
        private readonly IConfiguration _configuration;
        private readonly TseOptions _tseOptions;

        public ReceiptService(
            AppDbContext context,
            ILogger<ReceiptService> logger,
            ITseService tseService,
            ICompanyProfileProvider companyProfileProvider,
            IUserService userService,
            ISettingsTenantResolver settingsTenantResolver,
            IHostEnvironment hostEnvironment,
            IRksvEnvironmentService? rksvEnvironment = null,
            IConfiguration? configuration = null,
            IOptions<TseOptions>? tseOptions = null)
        {
            _context = context;
            _logger = logger;
            _tseService = tseService;
            _companyProfileProvider = companyProfileProvider;
            _userService = userService;
            _settingsTenantResolver = settingsTenantResolver;
            _hostEnvironment = hostEnvironment;
            _configuration = configuration ?? new ConfigurationBuilder().Build();
            _tseOptions = tseOptions?.Value ?? new TseOptions();
            _rksvEnvironment = rksvEnvironment
                               ?? new RksvEnvironmentService(_configuration, hostEnvironment);
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
                    && _context.CashRegisters.ForResolvedTenantScope().Any(cr => cr.Id == r.CashRegisterId && cr.TenantId == tenantId))
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
                    && _context.CashRegisters.ForResolvedTenantScope().Any(cr => cr.Id == r.CashRegisterId && cr.TenantId == tenantId))
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
                    && _context.CashRegisters.ForResolvedTenantScope().Any(cr => cr.Id == r.CashRegisterId && cr.TenantId == effectiveTenantId))
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
            var qrPayload = BuildReceiptQrPayload(signatureValue);

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
        public async Task<ReceiptDTO> GenerateReceiptAsync(PaymentDetails payment)
        {
            ArgumentNullException.ThrowIfNull(payment);

            var persisted = await GetReceiptByPaymentIdAsync(payment.Id).ConfigureAwait(false);
            if (persisted != null)
                return persisted;

            var registerNumberCtx = await ResolveRegisterNumberAsync(payment.CashRegisterId).ConfigureAwait(false);
            var signatureValue = payment.TseSignature ?? string.Empty;
            var prevSignatureValue = payment.PrevSignatureValueUsed
                ?? await GetLastSignatureValueForCashRegisterAsync(payment.CashRegisterId).ConfigureAwait(false);
            var certInfo = await _tseService.GetTseCertificateInfoAsync(registerNumberCtx).ConfigureAwait(false);
            var qrPayload = BuildReceiptQrPayload(signatureValue);

            var receipt = new Receipt
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
                CreatedAt = DateTime.UtcNow,
                Payment = payment,
            };

            ApplyPaymentItemsToReceipt(receipt, payment);

            var signatureDto = new ReceiptSignatureDTO
            {
                Algorithm = "ES256",
                SerialNumber = certInfo.CertificateNumber,
                Timestamp = payment.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                PrevSignatureValue = prevSignatureValue,
                SignatureValue = signatureValue,
                QrData = qrPayload,
            };

            return await MapToDtoAsync(receipt, signatureDto).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task AddReceiptFromPaymentToContextAsync(PaymentDetails payment)
        {
            var registerNumberCtx = await ResolveRegisterNumberAsync(payment.CashRegisterId);
            var signatureValue = payment.TseSignature ?? string.Empty;
            var prevSignatureValue = payment.PrevSignatureValueUsed ?? await GetLastSignatureValueForCashRegisterAsync(payment.CashRegisterId);
            var qrPayload = BuildReceiptQrPayload(signatureValue);

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

            ApplyPaymentItemsToReceipt(newReceipt, payment);
            _context.Receipts.Add(newReceipt);
        }

        private void ApplyPaymentItemsToReceipt(Receipt receipt, PaymentDetails payment)
        {
            var items = DeserializePaymentItems(payment);

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
                    ReceiptId = receipt.ReceiptId,
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
                            ReceiptId = receipt.ReceiptId,
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

            receipt.Items = receiptItems;
            receipt.SubTotal = receiptItems.Sum(x => x.LineNet);
            receipt.TaxTotal = receiptItems.Sum(x => x.VatAmount);
            receipt.GrandTotal = receiptItems.Sum(x => x.TotalPrice);

            receipt.TaxLines = taxLineInputs
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
        }

        private List<PaymentItem> DeserializePaymentItems(PaymentDetails payment)
        {
            try
            {
                if (payment.PaymentItems != null && payment.PaymentItems.RootElement.ValueKind != JsonValueKind.Undefined)
                    return JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.RootElement.GetRawText()) ?? new List<PaymentItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize items for payment {PaymentId}", payment.Id);
            }

            return new List<PaymentItem>();
        }

        /// <summary>RKSV §8 company header: payment snapshot first, then tenant <see cref="CompanySettings"/> (no demo defaults).</summary>
        private async Task<(string Name, string Address, string TaxNumber, string Footer)> ResolveReceiptCompanyContextAsync(
            PaymentDetails? payment)
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync().ConfigureAwait(false);
            var settings = await _context.CompanySettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId)
                .ConfigureAwait(false);

            // Use company settings from database, not hardcoded demo profile
            var companyName = !string.IsNullOrWhiteSpace(payment?.CompanyName)
                ? payment!.CompanyName!
                : settings?.CompanyName ?? string.Empty;
            var address = !string.IsNullOrWhiteSpace(payment?.CompanyAddress)
                ? payment!.CompanyAddress!
                : settings?.CompanyAddress ?? string.Empty;
            var taxNumber = !string.IsNullOrWhiteSpace(payment?.Steuernummer)
                ? payment!.Steuernummer!
                : settings?.CompanyTaxNumber ?? string.Empty;
            var footer = settings?.CompanyDescription ?? string.Empty;

            return (companyName, address, taxNumber, footer);
        }

        public async Task<PagedResult<ReceiptListItemDto>> GetReceiptListAsync(int page, int pageSize, string? sort, string? receiptNumber, string? cashRegisterId, string? cashierId, DateTime? issuedFrom, DateTime? issuedTo)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 100) pageSize = 100;

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
            var queryable = _context.Receipts.AsNoTracking()
                .Include(r => r.Payment)
                .Where(r => _context.CashRegisters.ForResolvedTenantScope().Any(cr => cr.Id == r.CashRegisterId && cr.TenantId == tenantId));

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
                    RksvSpecialReceiptYear = r.Payment != null ? r.Payment.RksvSpecialReceiptYear : null,
                    RksvSpecialReceiptMonth = r.Payment != null ? r.Payment.RksvSpecialReceiptMonth : null,
                    RksvFinanzOnlineSubmissionStatus = _context.RksvSpecialReceiptFinanzOnlineSubmissions
                        .Where(s => s.PaymentId == r.PaymentId)
                        .Select(s => s.Status)
                        .FirstOrDefault(),
                    IsLateCreated = r.Payment != null && r.Payment.IsLateCreated,
                    LateCreationReason = r.Payment != null ? r.Payment.LateCreationReason : null,
                    IntendedPeriodDate = r.Payment != null ? r.Payment.IntendedPeriodDate : null,
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

        /// <inheritdoc />
        public string GetTseSignatureDisplay(PaymentDetails payment)
        {
            ArgumentNullException.ThrowIfNull(payment);

            var signature = payment.TseSignature;
            if (string.IsNullOrEmpty(signature))
                return "TSE-Signatur: nicht verfügbar";

            var shortened = signature.Length > 50
                ? signature[..50] + "..."
                : signature;

            return $"TSE-Signatur:\n{shortened}";
        }

        /// <inheritdoc />
        public string GetRksvFooter(IHostEnvironment env)
        {
            ArgumentNullException.ThrowIfNull(env);

            var fiscal = FiscalEnvironmentResolver.Resolve(
                env,
                _tseOptions,
                _configuration,
                rksvEnvironment: _rksvEnvironment);

            return string.Equals(fiscal.RksvFooterLabel, DemoRksvFooterLabel, StringComparison.Ordinal)
                ? DemoRksvFooterLabel
                : ProductionRksvFooterLabel;
        }

        /// <summary>Receipt Kassierer line: login name preferred over raw user id (UUID).</summary>
        private static string? ResolveCashierDisplayName(ApplicationUser? appUser)
        {
            if (appUser == null)
                return null;

            // Use user name, not ID
            var cashierName = appUser.UserName ?? appUser.Email ?? appUser.Id;
            return string.IsNullOrWhiteSpace(cashierName) ? null : cashierName.Trim();
        }

        private async Task<ReceiptDTO> MapToDtoAsync(Receipt receipt, ReceiptSignatureDTO? explicitSig = null)
        {
            var cashierIdStr = receipt.CashierId ?? string.Empty;
            string? cashierDisplay = null;
            if (!string.IsNullOrEmpty(cashierIdStr))
            {
                var appUser = await _userService.GetUserByIdAsync(cashierIdStr).ConfigureAwait(false);
                cashierDisplay = ResolveCashierDisplayName(appUser);
            }

            var (companyName, companyAddress, companyTaxNumber, footerText) =
                await ResolveReceiptCompanyContextAsync(receipt.Payment).ConfigureAwait(false);

            var header = new ReceiptHeaderDTO
            {
                ShopName = companyName,
                Address = companyAddress
            };

            var company = new ReceiptCompanyDTO
            {
                Name = companyName,
                Address = companyAddress,
                TaxNumber = companyTaxNumber
            };

            var signature = explicitSig ?? new ReceiptSignatureDTO
            {
                Algorithm = "ES256",
                SerialNumber = "",
                Timestamp = receipt.IssuedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                PrevSignatureValue = receipt.PrevSignatureValue ?? "",
                SignatureValue = receipt.SignatureValue ?? "",
            };
            signature.QrData = ResolveQrDataForPrinting(receipt);

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
                FooterText = footerText,
                RksvFooterLabel = GetRksvFooter(_hostEnvironment),
            };
        }

        /// <summary>
        /// BMF §9 QR wire format from compact JWS (same builder as <see cref="PaymentService"/>).
        /// </summary>
        private static string BuildReceiptQrPayload(string? compactJws) =>
            RksvReceiptQrPayloadBuilder.BuildFromCompactJwsOrNull(compactJws) ?? string.Empty;

        /// <summary>
        /// Prefer freshly built §9 QR from the stored signature so reprints stay DEP-aligned;
        /// fall back to persisted <see cref="Receipt.QrCodePayload"/> for legacy rows.
        /// </summary>
        private static string ResolveQrDataForPrinting(Receipt receipt)
        {
            var fromSignature = RksvReceiptQrPayloadBuilder.BuildFromCompactJwsOrNull(receipt.SignatureValue);
            if (!string.IsNullOrEmpty(fromSignature))
                return fromSignature;

            return receipt.QrCodePayload ?? string.Empty;
        }

    }
}
