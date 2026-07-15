using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs.Rksv;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Rksv;

public sealed class MonatsbelegService : IMonatsbelegService
{
    private readonly AppDbContext _db;
    private readonly IDailyClosingService _dailyClosingService;
    private readonly ITseService _tseService;
    private readonly ITseKeyProvider _tseKeyProvider;
    private readonly IRksvEnvironmentService _rksvEnv;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MonatsbelegService> _logger;

    public MonatsbelegService(
        AppDbContext db,
        IDailyClosingService dailyClosingService,
        ITseService tseService,
        ITseKeyProvider tseKeyProvider,
        IRksvEnvironmentService rksvEnv,
        ICurrentUserService currentUserService,
        ILogger<MonatsbelegService> logger)
    {
        _db = db;
        _dailyClosingService = dailyClosingService;
        _tseService = tseService;
        _tseKeyProvider = tseKeyProvider;
        _rksvEnv = rksvEnv;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<MonatsbelegResult> CreateMonatsbelegAsync(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        ValidateMonth(year, month);

        if (await MonatsbelegExistsAsync(cashRegisterId, year, month, cancellationToken))
        {
            throw new InvalidOperationException($"Monatsbeleg for {year}-{month:00} already exists");
        }

        var register = await RequireRegisterAsync(cashRegisterId, cancellationToken);
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var targetMonthLocal = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var currentMonthLocal = new DateTime(viennaToday.Year, viennaToday.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);

        if (targetMonthLocal > currentMonthLocal)
        {
            throw new InvalidOperationException("Cannot create Monatsbeleg for a future Vienna calendar month");
        }

        var periodEndDay = targetMonthLocal.Year == viennaToday.Year && targetMonthLocal.Month == viennaToday.Month
            ? viennaToday
            : PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(
                year,
                month,
                DateTime.DaysInMonth(year, month));
        var (monthStartUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(targetMonthLocal);
        var (_, periodEndUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(periodEndDay);

        var paymentsWithoutInvoice = await CountPaymentsWithoutInvoiceAsync(
            cashRegisterId,
            monthStartUtc,
            periodEndUtc,
            cancellationToken);
        if (paymentsWithoutInvoice > 0)
        {
            throw new InvalidOperationException(
                $"Closing blocked: {paymentsWithoutInvoice} payment(s) without a matching invoice in the period.");
        }

        var aggregated = await MonatsbelegMonthlyAggregator.AggregateAsync(
            _dailyClosingService,
            _db.DailyClosings,
            register.TenantId,
            cashRegisterId,
            year,
            month,
            cancellationToken);

        if (aggregated.DailyClosingCount == 0)
        {
            throw new InvalidOperationException($"No daily closings found for {year}-{month:00}");
        }

        if (aggregated.TotalGross <= 0m && aggregated.TransactionCount == 0)
        {
            throw new InvalidOperationException($"No fiscal transactions found for {year}-{month:00}");
        }

        var summary = MapSummary(aggregated);
        var previousMonatsbeleg = await ResolvePreviousMonatsbelegAsync(
            cashRegisterId,
            year,
            month,
            cancellationToken);

        var previousSignature = previousMonatsbeleg?.TseSignature;
        var chainLength = await _db.Monatsbelege
            .CountAsync(m => m.CashRegisterId == cashRegisterId, cancellationToken) + 1;
        var isDemo = _rksvEnv.IsDemoMode() || _rksvEnv.IsTseSimulated();
        var signedAtUtc = DateTime.UtcNow;

        var tseSignature = await _tseService.CreateMonthlyClosingSignatureAsync(
            cashRegisterId,
            register.RegisterNumber,
            targetMonthLocal,
            summary.TotalGross,
            summary.TransactionCount);

        var actorId = _currentUserService.GetCurrentUserId();
        var actorUserId = actorId == Guid.Empty ? "system" : actorId.ToString();

        var dailyClosing = new DailyClosing
        {
            Id = Guid.NewGuid(),
            CashRegisterId = cashRegisterId,
            UserId = actorUserId,
            ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(targetMonthLocal),
            ClosingType = "Monthly",
            TotalAmount = summary.TotalGross,
            TotalTaxAmount = summary.TotalTax,
            TransactionCount = summary.TransactionCount,
            TseSignature = tseSignature,
            CertificateThumbprint = _tseKeyProvider.GetCurrentCertificateThumbprint(),
            Status = "Completed",
            CreatedAt = signedAtUtc,
        };

        await DailyClosingOperationalResolver.StampOperationalFieldsAsync(
            _db,
            dailyClosing,
            cashRegisterId,
            actorUserId,
            cancellationToken: cancellationToken);

        var monatsbeleg = new Monatsbeleg
        {
            CashRegisterId = cashRegisterId,
            Year = year,
            Month = month,
            TotalCash = summary.TotalCash,
            TotalCard = summary.TotalCard,
            TotalVoucher = summary.TotalVoucher,
            TotalOther = summary.TotalOther,
            TotalGross = summary.TotalGross,
            TotalTax = summary.TotalTax,
            TaxRate20 = summary.TaxRate20,
            TaxRate10 = summary.TaxRate10,
            TaxRate0 = summary.TaxRate0,
            TransactionCount = summary.TransactionCount,
            DailyClosingCount = summary.DailyClosingCount,
            TseSignature = tseSignature,
            TseSignatureTimestamp = signedAtUtc.ToString("O"),
            TseCertificateThumbprint = _tseKeyProvider.GetCurrentCertificateThumbprint(),
            PreviousSignature = previousSignature,
            SignatureChainLength = chainLength,
            IsSimulated = isDemo,
            Environment = isDemo ? "Demo" : "Production",
            CreatedByUserId = actorUserId,
            CreatedAtUtc = signedAtUtc,
            UpdatedAtUtc = signedAtUtc,
            DailyClosingId = dailyClosing.Id,
        };

        _db.DailyClosings.Add(dailyClosing);
        _db.Monatsbelege.Add(monatsbeleg);
        register.LastMonatsbelegUtc = signedAtUtc;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateMonatsbeleg(ex))
        {
            throw new InvalidOperationException($"Monatsbeleg for {year}-{month:00} already exists", ex);
        }

        _logger.LogInformation(
            "Monatsbeleg created for {Year}-{Month:00}, register {RegisterId}, simulated: {IsSimulated}",
            year,
            month,
            cashRegisterId,
            isDemo);

        monatsbeleg.CashRegister = register;
        return MapToResult(monatsbeleg);
    }

    public async Task<MonatsbelegResult> GetMonatsbelegAsync(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var monatsbeleg = await _db.Monatsbelege
            .Include(m => m.CashRegister)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.CashRegisterId == cashRegisterId && m.Year == year && m.Month == month,
                cancellationToken);

        if (monatsbeleg == null)
        {
            throw new KeyNotFoundException($"Monatsbeleg for {year}-{month:00} not found");
        }

        return MapToResult(monatsbeleg);
    }

    public async Task<List<MonatsbelegSummary>> GetMonatsbelegHistoryAsync(
        Guid cashRegisterId,
        int? year = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Monatsbelege.AsNoTracking()
            .Where(m => m.CashRegisterId == cashRegisterId);

        if (year.HasValue)
        {
            query = query.Where(m => m.Year == year.Value);
        }

        return await query
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Month)
            .Select(m => new MonatsbelegSummary
            {
                Year = m.Year,
                Month = m.Month,
                TotalGross = m.TotalGross,
                TotalTax = m.TotalTax,
                TransactionCount = m.TransactionCount,
                CreatedAt = m.CreatedAtUtc,
                IsSimulated = m.IsSimulated,
                HasSignature = m.TseSignature != null && m.TseSignature != string.Empty,
            })
            .ToListAsync(cancellationToken);
    }

    public Task<bool> MonatsbelegExistsAsync(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default) =>
        _db.Monatsbelege.AsNoTracking()
            .AnyAsync(
                m => m.CashRegisterId == cashRegisterId && m.Year == year && m.Month == month,
                cancellationToken);

    private MonatsbelegResult MapToResult(Monatsbeleg m) =>
        new()
        {
            Id = m.Id,
            CashRegisterId = m.CashRegisterId,
            CashRegisterName = m.CashRegister?.RegisterNumber ?? "Unknown",
            Year = m.Year,
            Month = m.Month,
            CreatedAt = m.CreatedAtUtc,
            TotalCash = m.TotalCash,
            TotalCard = m.TotalCard,
            TotalVoucher = m.TotalVoucher,
            TotalOther = m.TotalOther,
            TotalGross = m.TotalGross,
            TotalTax = m.TotalTax,
            TaxRate20 = m.TaxRate20,
            TaxRate10 = m.TaxRate10,
            TaxRate0 = m.TaxRate0,
            TransactionCount = m.TransactionCount,
            DailyClosingCount = m.DailyClosingCount,
            TseSignature = m.TseSignature,
            TseSignatureTimestamp = m.TseSignatureTimestamp,
            TseCertificateThumbprint = m.TseCertificateThumbprint,
            PreviousSignature = m.PreviousSignature,
            SignatureChainLength = m.SignatureChainLength,
            IsSimulated = m.IsSimulated,
            Environment = m.Environment,
            TseStatusDisplay = m.IsSimulated
                ? "TSE: SIMULIERT (NUR TEST)"
                : _rksvEnv.GetTseStatusDisplay(),
        };

    private static MonthlySummary MapSummary(Models.Reports.MonatsbelegSummaryDto dto) =>
        new()
        {
            TotalCash = dto.TotalCash,
            TotalCard = dto.TotalCard,
            TotalVoucher = dto.TotalVoucher,
            TotalOther = dto.TotalOther,
            TotalGross = dto.TotalGross,
            TotalTax = dto.TotalTax,
            TaxRate20 = dto.TaxRate20,
            TaxRate10 = dto.TaxRate10,
            TaxRate0 = dto.TaxRate0,
            TransactionCount = dto.TransactionCount,
            DailyClosingCount = dto.DailyClosingCount,
        };

    private async Task<Monatsbeleg?> ResolvePreviousMonatsbelegAsync(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var (prevYear, prevMonth) = month == 1 ? (year - 1, 12) : (year, month - 1);
        return await _db.Monatsbelege.AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.CashRegisterId == cashRegisterId && m.Year == prevYear && m.Month == prevMonth,
                cancellationToken);
    }

    private async Task<CashRegister> RequireRegisterAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var register = await _db.CashRegisters
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId, cancellationToken);

        if (register == null || register.Status == RegisterStatus.Decommissioned)
        {
            throw new InvalidOperationException($"Cash register {cashRegisterId} is not available.");
        }

        return register;
    }

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

    private static void ValidateMonth(int year, int month)
    {
        if (year < 2000 || year > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Year out of supported range.");
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be 1–12.");
        }
    }

    private static bool IsDuplicateMonatsbeleg(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("ix_monatsbeleg_per_register_month", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;
}
