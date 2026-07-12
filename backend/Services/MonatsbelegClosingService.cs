using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class MonatsbelegClosingService : IMonatsbelegClosingService
{
    private readonly AppDbContext _context;
    private readonly IDailyClosingService _dailyClosingService;
    private readonly ITseService _tseService;
    private readonly ITseKeyProvider _tseKeyProvider;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly TseOptions _tseOptions;
    private readonly IConfiguration _configuration;
    private readonly IRksvEnvironmentService _rksvEnvironment;
    private readonly ILogger<MonatsbelegClosingService> _logger;

    public MonatsbelegClosingService(
        AppDbContext context,
        IDailyClosingService dailyClosingService,
        ITseService tseService,
        ITseKeyProvider tseKeyProvider,
        ISettingsTenantResolver tenantResolver,
        IHostEnvironment hostEnvironment,
        IOptions<TseOptions> tseOptions,
        IConfiguration configuration,
        IRksvEnvironmentService rksvEnvironment,
        ILogger<MonatsbelegClosingService> logger)
    {
        _context = context;
        _dailyClosingService = dailyClosingService;
        _tseService = tseService;
        _tseKeyProvider = tseKeyProvider;
        _tenantResolver = tenantResolver;
        _hostEnvironment = hostEnvironment;
        _tseOptions = tseOptions.Value;
        _configuration = configuration;
        _rksvEnvironment = rksvEnvironment;
        _logger = logger;
    }

    public async Task<MonatsbelegSummaryDto> GenerateMonthlySummaryPreviewAsync(
        Guid tenantId,
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        ValidateMonth(year, month);
        await EnsureRegisterAsync(tenantId, cashRegisterId, cancellationToken);

        return await MonatsbelegMonthlyAggregator.AggregateAsync(
            _dailyClosingService,
            _context.DailyClosings,
            tenantId,
            cashRegisterId,
            year,
            month,
            cancellationToken);
    }

    public async Task<MonatsbelegClosingResult> CreateMonatsbelegClosingAsync(
        string actorUserId,
        CreateMonatsbelegClosingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));

        var (year, month) = ResolveTargetPeriod(request.Year, request.Month);
        ValidateMonth(year, month);

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var register = await EnsureRegisterAsync(tenantId, request.CashRegisterId, cancellationToken);

        if (register.Status == RegisterStatus.Decommissioned)
        {
            return Fail("Cash register is not available for monthly closing");
        }

        var duplicate = await _context.Set<Monatsbeleg>().AsNoTracking()
            .AnyAsync(
                m => m.CashRegisterId == request.CashRegisterId && m.Year == year && m.Month == month,
                cancellationToken);
        if (duplicate)
        {
            return Fail($"Monthly closing already exists for {year}-{month:00}");
        }

        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var currentMonthLocal = new DateTime(viennaToday.Year, viennaToday.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var targetMonthLocal = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        if (targetMonthLocal > currentMonthLocal)
        {
            return Fail("Cannot create Monatsbeleg for a future Vienna calendar month");
        }

        var (monthStartUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(targetMonthLocal);
        var periodEndDay = targetMonthLocal.Year == viennaToday.Year && targetMonthLocal.Month == viennaToday.Month
            ? viennaToday
            : PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(year, month, DateTime.DaysInMonth(year, month));
        var (_, periodEndUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(periodEndDay);

        var paymentsWithoutInvoiceCount = await CountPaymentsWithoutInvoiceAsync(
            request.CashRegisterId,
            monthStartUtc,
            periodEndUtc,
            cancellationToken);
        if (paymentsWithoutInvoiceCount > 0)
        {
            return Fail(
                $"Closing blocked: {paymentsWithoutInvoiceCount} payment(s) without a matching invoice in the period.");
        }

        var summary = await GenerateMonthlySummaryPreviewAsync(
            tenantId,
            request.CashRegisterId,
            year,
            month,
            cancellationToken);

        if (summary.DailyClosingCount == 0)
        {
            return Fail("No completed daily closings found for the target month");
        }

        if (summary.TotalGross <= 0m && summary.TransactionCount == 0)
        {
            return Fail("No fiscal transactions found for the target month");
        }

        var previousSignature = await ResolvePreviousMonthSignatureAsync(
            request.CashRegisterId,
            year,
            month,
            cancellationToken);

        var chainLength = await _context.Set<Monatsbeleg>()
            .CountAsync(m => m.CashRegisterId == request.CashRegisterId, cancellationToken) + 1;

        var fiscalEnvironment = FiscalEnvironmentResolver.Resolve(
            _hostEnvironment,
            _tseOptions,
            _configuration,
            rksvEnvironment: _rksvEnvironment);

        string tseSignature;
        try
        {
            tseSignature = await _tseService.CreateMonthlyClosingSignatureAsync(
                request.CashRegisterId,
                register.RegisterNumber,
                targetMonthLocal,
                summary.TotalGross,
                summary.TransactionCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Monatsbeleg TSE signing failed for register {RegisterId} {Year}-{Month}",
                request.CashRegisterId, year, month);
            return Fail(ex.Message);
        }

        var signedAtUtc = DateTime.UtcNow;
        var thumbprint = _tseKeyProvider.GetCurrentCertificateThumbprint();

        var dailyClosing = new DailyClosing
        {
            Id = Guid.NewGuid(),
            CashRegisterId = request.CashRegisterId,
            UserId = actorUserId,
            ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(targetMonthLocal),
            ClosingType = "Monthly",
            TotalAmount = summary.TotalGross,
            TotalTaxAmount = summary.TotalTax,
            TransactionCount = summary.TransactionCount,
            TseSignature = tseSignature,
            CertificateThumbprint = thumbprint,
            Status = "Completed",
            CreatedAt = signedAtUtc,
        };

        var monatsbeleg = new Monatsbeleg
        {
            CashRegisterId = request.CashRegisterId,
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
            TseCertificateThumbprint = thumbprint,
            PreviousSignature = previousSignature,
            SignatureChainLength = chainLength,
            IsSimulated = fiscalEnvironment.IsDemoFiscal,
            Environment = fiscalEnvironment.EnvironmentName,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = signedAtUtc,
            UpdatedAtUtc = signedAtUtc,
            DailyClosingId = dailyClosing.Id,
        };

        _context.DailyClosings.Add(dailyClosing);
        _context.Set<Monatsbeleg>().Add(monatsbeleg);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateClosing(ex))
        {
            return Fail($"Monthly closing already exists for {year}-{month:00}");
        }

        var report = MonatsbelegReportComposer.Compose(
            monatsbeleg,
            summary,
            register.RegisterNumber,
            fiscalEnvironment);

        _logger.LogInformation(
            "Monatsbeleg created Id={MonatsbelegId} Register={RegisterId} Period={Year}-{Month} Simulated={Simulated}",
            monatsbeleg.Id,
            request.CashRegisterId,
            year,
            month,
            fiscalEnvironment.IsDemoFiscal);

        return new MonatsbelegClosingResult
        {
            Success = true,
            MonatsbelegId = monatsbeleg.Id,
            DailyClosingId = dailyClosing.Id,
            Year = year,
            Month = month,
            TotalGross = summary.TotalGross,
            TotalTax = summary.TotalTax,
            TransactionCount = summary.TransactionCount,
            DailyClosingCount = summary.DailyClosingCount,
            TseSignature = tseSignature,
            PreviousSignature = previousSignature,
            IsSimulated = fiscalEnvironment.IsDemoFiscal,
            Environment = fiscalEnvironment.EnvironmentName,
            Report = report,
        };
    }

    public async Task<MonatsbelegDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<Monatsbeleg>().AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (entity == null)
            return null;

        var registerNumber = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.Id == entity.CashRegisterId)
            .Select(r => r.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var summary = BuildSummaryFromEntity(entity);
        var fiscalEnvironment = FiscalEnvironmentResolver.Resolve(
            _hostEnvironment,
            _tseOptions,
            _configuration,
            rksvEnvironment: _rksvEnvironment);

        return MonatsbelegReportComposer.ToDetailDto(entity, summary, registerNumber, fiscalEnvironment);
    }

    public async Task<IReadOnlyList<MonatsbelegListItemDto>> ListAsync(
        Guid? cashRegisterId,
        int? year,
        int? month,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Monatsbeleg>().AsNoTracking().AsQueryable();
        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
            query = query.Where(m => m.CashRegisterId == cashRegisterId.Value);
        if (year.HasValue)
            query = query.Where(m => m.Year == year.Value);
        if (month.HasValue)
            query = query.Where(m => m.Month == month.Value);

        var rows = await query
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Month)
            .ThenByDescending(m => m.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        var regIds = rows.Select(r => r.CashRegisterId).Distinct().ToList();
        var regs = await _context.CashRegisters.AsNoTracking()
            .Where(r => regIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.RegisterNumber, cancellationToken);

        return rows.Select(r => new MonatsbelegListItemDto
        {
            Id = r.Id,
            CashRegisterId = r.CashRegisterId,
            RegisterNumber = regs.GetValueOrDefault(r.CashRegisterId),
            Year = r.Year,
            Month = r.Month,
            TotalGross = r.TotalGross,
            TransactionCount = r.TransactionCount,
            IsSimulated = r.IsSimulated,
            Environment = r.Environment,
            CreatedAtUtc = r.CreatedAtUtc,
        }).ToList();
    }

    public async Task<PosDailyClosingReportDto?> BuildReportDtoAsync(
        Guid monatsbelegId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<Monatsbeleg>().AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == monatsbelegId, cancellationToken);
        if (entity == null)
            return null;

        var registerNumber = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.Id == entity.CashRegisterId)
            .Select(r => r.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var summary = BuildSummaryFromEntity(entity);
        var fiscalEnvironment = FiscalEnvironmentResolver.Resolve(
            _hostEnvironment,
            _tseOptions,
            _configuration,
            rksvEnvironment: _rksvEnvironment);

        return MonatsbelegReportComposer.Compose(entity, summary, registerNumber, fiscalEnvironment);
    }

    public async Task<bool> CanCreateForCurrentMonthAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var reg = await _context.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId, cancellationToken);
        if (reg == null || reg.Status == RegisterStatus.Decommissioned)
            return false;

        var (year, month) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var exists = await _context.Set<Monatsbeleg>().AsNoTracking()
            .AnyAsync(m => m.CashRegisterId == cashRegisterId && m.Year == year && m.Month == month, cancellationToken);
        return !exists;
    }

    private static MonatsbelegSummaryDto BuildSummaryFromEntity(Monatsbeleg entity) =>
        new()
        {
            Year = entity.Year,
            Month = entity.Month,
            CashRegisterId = entity.CashRegisterId,
            DailyClosingCount = entity.DailyClosingCount,
            TotalCash = entity.TotalCash,
            TotalCard = entity.TotalCard,
            TotalVoucher = entity.TotalVoucher,
            TotalOther = entity.TotalOther,
            TotalGross = entity.TotalGross,
            TotalTax = entity.TotalTax,
            TaxRate20 = entity.TaxRate20,
            TaxRate10 = entity.TaxRate10,
            TaxRate0 = entity.TaxRate0,
            TransactionCount = entity.TransactionCount,
            PaymentBreakdown = PaymentBreakdown.FromAmounts(
                entity.TotalCash,
                entity.TotalCard,
                entity.TotalVoucher,
                entity.TotalOther),
            TaxBreakdown = new DailyClosingTaxBreakdownDto
            {
                TaxAt20 = entity.TaxRate20,
                TaxAt10 = entity.TaxRate10,
                GrossAt0 = entity.TaxRate0,
            },
        };

    private async Task<string?> ResolvePreviousMonthSignatureAsync(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var (prevYear, prevMonth) = month == 1 ? (year - 1, 12) : (year, month - 1);
        return await _context.Set<Monatsbeleg>().AsNoTracking()
            .Where(m => m.CashRegisterId == cashRegisterId && m.Year == prevYear && m.Month == prevMonth)
            .Select(m => m.TseSignature)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<int> CountPaymentsWithoutInvoiceAsync(
        Guid cashRegisterId,
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken cancellationToken)
    {
        fromInclusive = PostgreSqlUtcDateTime.ToUtcForNpgsql(fromInclusive);
        toExclusive = PostgreSqlUtcDateTime.ToUtcForNpgsql(toExclusive);

        return await _context.PaymentDetails.AsNoTracking()
            .Where(p => p.CreatedAt >= fromInclusive
                        && p.CreatedAt < toExclusive
                        && p.IsActive
                        && p.CashRegisterId == cashRegisterId
                        && !_context.Invoices.Any(i => i.SourcePaymentId == p.Id))
            .CountAsync(cancellationToken);
    }

    private async Task<CashRegister> EnsureRegisterAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var register = await _context.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId && r.TenantId == tenantId, cancellationToken);
        if (register == null)
            throw new InvalidOperationException("Cash register not found for the current tenant.");
        return register;
    }

    private static (int Year, int Month) ResolveTargetPeriod(int? year, int? month)
    {
        var (currentYear, currentMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        return (year ?? currentYear, month ?? currentMonth);
    }

    private static void ValidateMonth(int year, int month)
    {
        if (year < 2000 || year > 2100)
            throw new ArgumentOutOfRangeException(nameof(year), "Year out of supported range.");
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be 1–12.");
    }

    private static MonatsbelegClosingResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };

    private static bool IsDuplicateClosing(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("ix_monatsbeleg_per_register_month", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("ix_daily_closings", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;
}
