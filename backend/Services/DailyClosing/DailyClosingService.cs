using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IDailyClosingService
{
    /// <summary>
    /// Builds a read-only payment-row snapshot for one Austria business day and tenant scope.
    /// </summary>
    Task<DailyClosingSummaryDto> GenerateClosingSummaryAsync(
        Guid tenantId,
        Guid? cashRegisterId,
        DateTime businessDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a fiscal daily closing for the current Vienna business day with TSE signature and RKSV Phase 1 metadata.
    /// </summary>
    Task<DailyClosingResult> CreateDailyClosingAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);
}

public sealed class DailyClosingService : IDailyClosingService
{
    private readonly AppDbContext _db;
    private readonly ITseService _tseService;
    private readonly ITseKeyProvider _tseKeyProvider;
    private readonly IRksvEnvironmentService _rksvEnv;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISettingsTenantResolver _tenantResolver;

    public DailyClosingService(
        AppDbContext db,
        ITseService tseService,
        ITseKeyProvider tseKeyProvider,
        IRksvEnvironmentService rksvEnv,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        ISettingsTenantResolver tenantResolver)
    {
        _db = db;
        _tseService = tseService;
        _tseKeyProvider = tseKeyProvider;
        _rksvEnv = rksvEnv;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _tenantResolver = tenantResolver;
    }

    /// <inheritdoc />
    public async Task<DailyClosingSummaryDto> GenerateClosingSummaryAsync(
        Guid tenantId,
        Guid? cashRegisterId,
        DateTime businessDate,
        CancellationToken cancellationToken = default)
    {
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(
            businessDate.Year, businessDate.Month, businessDate.Day);
        var (fromUtc, toExclusive) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(day);

        var registerQuery = _db.CashRegisters.AsNoTracking().ForResolvedTenantScope().Where(cr => cr.TenantId == tenantId);
        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
            registerQuery = registerQuery.Where(cr => cr.Id == cashRegisterId.Value);

        var registerIds = await registerQuery.Select(cr => cr.Id).ToListAsync(cancellationToken);
        if (registerIds.Count == 0)
        {
            return new DailyClosingSummaryDto
            {
                BusinessDate = day,
                CashRegisterId = cashRegisterId,
            };
        }

        var payments = await _db.PaymentDetails.AsNoTracking()
            .Where(p => registerIds.Contains(p.CashRegisterId)
                        && p.CreatedAt >= fromUtc
                        && p.CreatedAt < toExclusive
                        && p.IsActive)
            .ToListAsync(cancellationToken);

        var saleLike = payments
            .Where(p => !p.IsRefund && !p.IsStorno && p.RksvSpecialReceiptKind == null)
            .ToList();

        var fiscalInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => registerIds.Contains(i.CashRegisterId)
                        && i.CreatedAt >= fromUtc
                        && i.CreatedAt < toExclusive
                        && i.Status == InvoiceStatus.Paid)
            .Where(i => i.SourcePaymentId == null
                        || !_db.PaymentDetails.Any(p =>
                            p.Id == i.SourcePaymentId!.Value
                            && p.RksvSpecialReceiptKind != null))
            .ToListAsync(cancellationToken);

        var paymentSalesTotal = saleLike.Sum(p => p.TotalAmount);
        var fiscalTotalAmount = fiscalInvoices.Sum(i => i.TotalAmount);

        var special = payments.Where(p => p.RksvSpecialReceiptKind != null)
            .OrderBy(p => p.CreatedAt)
            .Select(MapLine)
            .ToList();

        var stornoRows = payments.Where(p => p.IsStorno)
            .OrderBy(p => p.CreatedAt)
            .Select(MapLine)
            .ToList();

        decimal SumForMethod(IEnumerable<PaymentDetails> rows, PaymentMethod method)
        {
            var raw = ((int)method).ToString();
            return rows.Where(p => p.PaymentMethodRaw == raw).Sum(p => p.TotalAmount);
        }

        static int CountForMethod(IEnumerable<PaymentDetails> rows, PaymentMethod method)
        {
            var raw = ((int)method).ToString();
            return rows.Count(p => p.PaymentMethodRaw == raw);
        }

        var cashCount = CountForMethod(saleLike, PaymentMethod.Cash);
        var cardCount = CountForMethod(saleLike, PaymentMethod.Card);
        var voucherCount = CountForMethod(saleLike, PaymentMethod.Voucher);

        var taxBreakdown = DailyClosingTaxBreakdownAggregator.AggregateFromTaxDetailsJsonDocuments(
            fiscalInvoices.Select(i => i.TaxDetails));

        var totalCash = SumForMethod(saleLike, PaymentMethod.Cash);
        var totalCard = SumForMethod(saleLike, PaymentMethod.Card);
        var totalVoucher = SumForMethod(saleLike, PaymentMethod.Voucher);
        var totalOther = saleLike
            .Where(p =>
            {
                if (!int.TryParse(p.PaymentMethodRaw, out var v) || !Enum.IsDefined(typeof(PaymentMethod), v))
                    return true;
                var m = (PaymentMethod)v;
                return m is not (PaymentMethod.Cash or PaymentMethod.Card or PaymentMethod.Voucher);
            })
            .Sum(p => p.TotalAmount);

        return new DailyClosingSummaryDto
        {
            BusinessDate = day,
            CashRegisterId = cashRegisterId,
            TotalSales = paymentSalesTotal,
            TotalCash = totalCash,
            TotalCard = totalCard,
            TotalVoucherRedemptions = totalVoucher,
            PaymentBreakdown = PaymentBreakdown.FromAmounts(totalCash, totalCard, totalVoucher, totalOther),
            FiscalTotalAmount = fiscalTotalAmount,
            FiscalTotalTaxAmount = fiscalInvoices.Sum(i => i.TaxAmount),
            FiscalTransactionCount = fiscalInvoices.Count,
            SalesFiscalDelta = paymentSalesTotal - fiscalTotalAmount,
            TotalOtherPaymentMethods = totalOther,
            ReceiptCount = saleLike.Count,
            StornoRowCount = stornoRows.Count,
            StornoTotalAmount = payments.Where(p => p.IsStorno).Sum(p => p.TotalAmount),
            SpecialReceipts = special,
            Stornos = stornoRows,
            TransactionBreakdown = new TransactionBreakdown
            {
                Cash = cashCount,
                Card = cardCount,
                Voucher = voucherCount,
                Cancellations = stornoRows.Count,
                Total = saleLike.Count,
            },
            TaxBreakdown = taxBreakdown,
        };
    }

    /// <inheritdoc />
    public async Task<DailyClosingResult> CreateDailyClosingAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
        {
            return Fail("Cash register id is required.");
        }

        var actorUserId = _httpContextAccessor.HttpContext?.User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(actorUserId) && _currentUserService.GetCurrentUserId() == Guid.Empty)
        {
            return Fail("Authenticated user is required to create a daily closing.");
        }

        actorUserId ??= _currentUserService.GetCurrentUserId().ToString();
        if (string.IsNullOrWhiteSpace(actorUserId) || actorUserId == Guid.Empty.ToString())
        {
            return Fail("Authenticated user is required to create a daily closing.");
        }

        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId, cancellationToken);
        if (register == null)
        {
            return Fail("Cash register not found.");
        }

        if (register.Status == RegisterStatus.Decommissioned)
        {
            return Fail("Cash register is not available for daily closing.");
        }

        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var closingAnchorUtc = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(viennaToday);
        var (dayStartUtc, dayEndExclusiveUtc) =
            PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);

        var lastClosingDate = await _db.DailyClosings.AsNoTracking()
            .Where(c => c.CashRegisterId == cashRegisterId
                        && c.ClosingType == "Daily"
                        && c.Status == "Completed")
            .OrderByDescending(c => c.ClosingDate)
            .Select(c => (DateTime?)c.ClosingDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastClosingDate.HasValue)
        {
            var lastViennaDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(lastClosingDate.Value);
            if (lastViennaDay >= viennaToday)
            {
                return Fail("Daily closing already performed for today.");
            }
        }

        var paymentsWithoutInvoiceCount = await CountPaymentsWithoutInvoiceAsync(
            cashRegisterId,
            dayStartUtc,
            dayEndExclusiveUtc,
            cancellationToken);
        if (paymentsWithoutInvoiceCount > 0)
        {
            return Fail(
                $"Closing blocked: {paymentsWithoutInvoiceCount} payment(s) without a matching invoice.");
        }

        var transactions = await _db.Invoices.AsNoTracking()
            .Where(i => i.CashRegisterId == cashRegisterId
                        && i.CreatedAt >= dayStartUtc
                        && i.CreatedAt < dayEndExclusiveUtc
                        && i.Status == InvoiceStatus.Paid)
            .Where(i => i.SourcePaymentId == null
                        || !_db.PaymentDetails.Any(p =>
                            p.Id == i.SourcePaymentId!.Value
                            && p.RksvSpecialReceiptKind != null))
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
        {
            return Fail("No transactions found for today. Cannot perform daily closing.");
        }

        var tenantId = register.TenantId != Guid.Empty
            ? register.TenantId
            : await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

        var summary = await GenerateClosingSummaryAsync(
            tenantId,
            cashRegisterId,
            viennaToday,
            cancellationToken);

        var totalAmount = transactions.Sum(t => t.TotalAmount);
        var totalTaxAmount = transactions.Sum(t => t.TaxAmount);
        var transactionCount = transactions.Count;

        var previousClosing = await _db.DailyClosings.AsNoTracking()
            .Where(c => c.CashRegisterId == cashRegisterId
                        && c.ClosingType == "Daily"
                        && c.Status == "Completed")
            .OrderByDescending(c => c.ClosingDate)
            .FirstOrDefaultAsync(cancellationToken);

        var previousSignature = previousClosing?.TseSignature;
        var chainLength = (previousClosing?.SignatureChainLength ?? 0) + 1;
        var isDemo = _rksvEnv.IsTseSimulated();
        var environmentName = isDemo ? "Demo" : "Production";

        string tseSignature;
        try
        {
            tseSignature = await _tseService.CreateDailyClosingSignatureAsync(
                cashRegisterId,
                register.RegisterNumber,
                viennaToday,
                totalAmount,
                transactionCount);
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }

        var signedAtUtc = DateTime.UtcNow;
        var thumbprint = _tseKeyProvider.GetCurrentCertificateThumbprint();

        var closing = new DailyClosing
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = cashRegisterId,
            UserId = actorUserId,
            ClosingDate = closingAnchorUtc,
            ClosingType = "Daily",
            TotalAmount = totalAmount,
            TotalTaxAmount = totalTaxAmount,
            TransactionCount = transactionCount,
            TseSignature = tseSignature,
            TseSignatureTimestamp = signedAtUtc.ToString("O"),
            CertificateThumbprint = thumbprint,
            PreviousSignature = previousSignature,
            SignatureChainLength = chainLength,
            IsSimulated = isDemo,
            Environment = environmentName,
            Status = "Completed",
            CreatedAt = signedAtUtc,
        };

        await DailyClosingOperationalResolver.StampOperationalFieldsAsync(
            _db,
            closing,
            cashRegisterId,
            actorUserId,
            cancellationToken: cancellationToken);

        _db.DailyClosings.Add(closing);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateDailyClosing(ex))
        {
            return Fail("Daily closing already performed for today.");
        }

        return new DailyClosingResult
        {
            Success = true,
            Closing = MapToDto(closing, register.RegisterNumber),
            PaymentBreakdown = summary.PaymentBreakdown,
            TaxBreakdown = summary.TaxBreakdown,
        };
    }

    private DailyClosingDto MapToDto(DailyClosing closing, string? registerNumber) =>
        new()
        {
            Id = closing.Id,
            CashRegisterId = closing.CashRegisterId,
            RegisterNumber = registerNumber,
            UserId = closing.UserId,
            ClosingDate = closing.ClosingDate,
            ClosingType = closing.ClosingType,
            TotalAmount = closing.TotalAmount,
            TotalTaxAmount = closing.TotalTaxAmount,
            TransactionCount = closing.TransactionCount,
            Status = closing.Status,
            FinanzOnlineStatus = closing.FinanzOnlineStatus,
            CreatedAt = closing.CreatedAt,
            TseSignature = closing.TseSignature ?? string.Empty,
            TseSignatureTimestamp = closing.TseSignatureTimestamp ?? string.Empty,
            TseCertificateThumbprint = closing.CertificateThumbprint ?? string.Empty,
            PreviousSignature = closing.PreviousSignature ?? string.Empty,
            SignatureChainLength = closing.SignatureChainLength,
            IsSimulated = closing.IsSimulated,
            Environment = closing.Environment ?? string.Empty,
            TseStatusDisplay = _rksvEnv.GetTseStatusDisplay(),
            RksvFooter = _rksvEnv.GetRksvFooter(),
        };

    private async Task<int> CountPaymentsWithoutInvoiceAsync(
        Guid cashRegisterId,
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken cancellationToken)
    {
        fromInclusive = PostgreSqlUtcDateTime.ToUtcForNpgsql(fromInclusive);
        toExclusive = PostgreSqlUtcDateTime.ToUtcForNpgsql(toExclusive);

        return await _db.PaymentDetails.AsNoTracking()
            .Where(p => p.CreatedAt >= fromInclusive
                        && p.CreatedAt < toExclusive
                        && p.IsActive
                        && p.CashRegisterId == cashRegisterId
                        && !_db.Invoices.Any(i => i.SourcePaymentId == p.Id))
            .CountAsync(cancellationToken);
    }

    private static bool IsDuplicateDailyClosing(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("IX_", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;

    private static DailyClosingResult Fail(string message) =>
        new()
        {
            Success = false,
            ErrorMessage = message,
        };

    private static DailyClosingSummaryLineDto MapLine(PaymentDetails p) => new()
    {
        Id = p.Id,
        CashRegisterId = p.CashRegisterId,
        CreatedAtUtc = p.CreatedAt,
        ReceiptNumber = p.ReceiptNumber,
        TotalAmount = p.TotalAmount,
        PaymentMethod = ResolvePaymentMethodName(p.PaymentMethodRaw),
        RksvSpecialReceiptKind = p.RksvSpecialReceiptKind,
        IsStorno = p.IsStorno,
        StornoReason = p.StornoReason is null ? null : p.StornoReason.ToString(),
        OriginalReceiptId = p.OriginalReceiptId,
    };

    private static string ResolvePaymentMethodName(string rawValue)
    {
        if (int.TryParse(rawValue, out var methodInt) && Enum.IsDefined(typeof(PaymentMethod), methodInt))
            return ((PaymentMethod)methodInt).ToString();
        return "Unknown";
    }
}
