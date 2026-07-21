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
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class JahresbelegClosingService : IJahresbelegClosingService
{
    private readonly AppDbContext _context;
    private readonly ITseService _tseService;
    private readonly ITseKeyProvider _tseKeyProvider;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly TseOptions _tseOptions;
    private readonly IConfiguration _configuration;
    private readonly IRksvEnvironmentService _rksvEnvironment;
    private readonly IJahresbelegReportService _reportService;
    private readonly ILogger<JahresbelegClosingService> _logger;
    private readonly IReportPdfCaptureService _reportPdfCapture;

    public JahresbelegClosingService(
        AppDbContext context,
        ITseService tseService,
        ITseKeyProvider tseKeyProvider,
        ISettingsTenantResolver tenantResolver,
        IHostEnvironment hostEnvironment,
        IOptions<TseOptions> tseOptions,
        IConfiguration configuration,
        IRksvEnvironmentService rksvEnvironment,
        IJahresbelegReportService reportService,
        ILogger<JahresbelegClosingService> logger,
        IReportPdfCaptureService reportPdfCapture)
    {
        _context = context;
        _tseService = tseService;
        _tseKeyProvider = tseKeyProvider;
        _tenantResolver = tenantResolver;
        _hostEnvironment = hostEnvironment;
        _tseOptions = tseOptions.Value;
        _configuration = configuration;
        _rksvEnvironment = rksvEnvironment;
        _reportService = reportService;
        _logger = logger;
        _reportPdfCapture = reportPdfCapture;
    }

    public async Task<JahresbelegSummaryDto> GenerateYearlySummaryPreviewAsync(
        Guid tenantId,
        Guid cashRegisterId,
        int year,
        CancellationToken cancellationToken = default)
    {
        ValidateYear(year);
        await EnsureRegisterAsync(tenantId, cashRegisterId, cancellationToken);
        var decemberAsJb = await GetDecemberMonatsbelegCountsAsJahresbelegAsync(tenantId, cancellationToken);

        return await JahresbelegYearlyAggregator.AggregateAsync(
            _context.Monatsbelege,
            cashRegisterId,
            year,
            decemberAsJb,
            cancellationToken);
    }

    public async Task<JahresbelegClosingResult> CreateJahresbelegClosingAsync(
        string actorUserId,
        CreateJahresbelegClosingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));

        var year = ResolveTargetYear(request.Year);
        ValidateYear(year);

        var viennaYear = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth().Year;
        if (year < viennaYear - 1 || year > viennaYear)
        {
            return Fail($"Jahresbeleg can only be created for Vienna year {viennaYear} or {viennaYear - 1}, not {year}.");
        }

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var register = await EnsureRegisterAsync(tenantId, request.CashRegisterId, cancellationToken);

        if (register.Status == RegisterStatus.Decommissioned)
            return Fail("Cash register is not available for yearly closing");

        var duplicate = await _context.Jahresbelege.AsNoTracking()
            .AnyAsync(j => j.CashRegisterId == request.CashRegisterId && j.Year == year, cancellationToken);
        if (duplicate)
            return Fail($"Yearly closing already exists for {year}");

        var decemberAsJb = await GetDecemberMonatsbelegCountsAsJahresbelegAsync(tenantId, cancellationToken);
        var summary = await GenerateYearlySummaryPreviewAsync(
            tenantId,
            request.CashRegisterId,
            year,
            cancellationToken);

        if (summary.MonatsbelegCount == 0)
            return Fail("No Monatsbeleg rows found for the target year");

        if (summary.MissingMonths.Count > 0)
        {
            return Fail(
                $"Missing Monatsbeleg for month(s): {string.Join(", ", summary.MissingMonths.Select(m => m.ToString("00")))}");
        }

        if (summary.TotalGross <= 0m && summary.TransactionCount == 0)
            return Fail("No fiscal transactions found for the target year");

        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var targetYearLocal = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        if (targetYearLocal.Year > viennaToday.Year)
            return Fail("Cannot create Jahresbeleg for a future Vienna calendar year");

        var (yearStartUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(targetYearLocal);
        var periodEndDay = year < viennaToday.Year
            ? PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(year, 12, 31)
            : viennaToday;
        var (_, periodEndUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(periodEndDay);

        var paymentsWithoutInvoiceCount = await CountPaymentsWithoutInvoiceAsync(
            request.CashRegisterId,
            yearStartUtc,
            periodEndUtc,
            cancellationToken);
        if (paymentsWithoutInvoiceCount > 0)
        {
            return Fail(
                $"Closing blocked: {paymentsWithoutInvoiceCount} payment(s) without a matching invoice in the period.");
        }

        var previousSignature = await ResolvePreviousYearSignatureAsync(
            request.CashRegisterId,
            year,
            cancellationToken);

        var chainLength = await _context.Jahresbelege
            .CountAsync(j => j.CashRegisterId == request.CashRegisterId, cancellationToken) + 1;

        var fiscalEnvironment = FiscalEnvironmentResolver.Resolve(
            _hostEnvironment,
            _tseOptions,
            _configuration,
            rksvEnvironment: _rksvEnvironment);

        await using var fiscalTx = await _context.Database.BeginTransactionAsync(cancellationToken);
        string tseSignature;
        DateTime signedAtUtc;
        string? thumbprint;
        DailyClosing dailyClosing;
        Jahresbeleg jahresbeleg;
        try
        {
            tseSignature = await _tseService.CreateYearlyClosingSignatureAsync(
                request.CashRegisterId,
                register.RegisterNumber,
                targetYearLocal,
                summary.TotalGross,
                summary.TransactionCount,
                fiscalTx);

            signedAtUtc = DateTime.UtcNow;
            thumbprint = _tseKeyProvider.GetCurrentCertificateThumbprint();
            var monthlyReferencesJson = JahresbelegYearlyAggregator.SerializeMonthlyReferences(summary.MonthlyReferences);

            dailyClosing = new DailyClosing
            {
                Id = Guid.NewGuid(),
                CashRegisterId = request.CashRegisterId,
                UserId = actorUserId,
                ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(targetYearLocal),
                ClosingType = "Yearly",
                TotalAmount = summary.TotalGross,
                TotalTaxAmount = summary.TotalTax,
                TransactionCount = summary.TransactionCount,
                TseSignature = tseSignature,
                CertificateThumbprint = thumbprint,
                Status = "Completed",
                CreatedAt = signedAtUtc,
            };

            await DailyClosingOperationalResolver.StampOperationalFieldsAsync(
                _context,
                dailyClosing,
                request.CashRegisterId,
                actorUserId,
                cancellationToken: cancellationToken);

            jahresbeleg = new Jahresbeleg
            {
                CashRegisterId = request.CashRegisterId,
                Year = year,
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
                MonthlyReferences = monthlyReferencesJson,
                TseSignature = tseSignature,
                TseSignatureTimestamp = signedAtUtc.ToString("O"),
                TseCertificateThumbprint = thumbprint,
                PreviousSignature = previousSignature,
                SignatureChainLength = chainLength,
                IsSimulated = fiscalEnvironment.IsDemoFiscal,
                Environment = fiscalEnvironment.EnvironmentName,
                IsDecemberMonatsbeleg = summary.IsDecemberMonatsbeleg,
                CreatedByUserId = actorUserId,
                CreatedAtUtc = signedAtUtc,
                UpdatedAtUtc = signedAtUtc,
                DailyClosingId = dailyClosing.Id,
            };

            _context.DailyClosings.Add(dailyClosing);
            _context.Jahresbelege.Add(jahresbeleg);

            await _context.SaveChangesAsync(cancellationToken);

            var cashReg = await _context.CashRegisters
                .FirstAsync(r => r.Id == request.CashRegisterId, cancellationToken);
            cashReg.LastJahresbelegUtc = signedAtUtc;
            await _context.SaveChangesAsync(cancellationToken);
            await fiscalTx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateClosing(ex))
        {
            await fiscalTx.RollbackAsync(cancellationToken);
            return Fail($"Yearly closing already exists for {year}");
        }
        catch (Exception ex)
        {
            await fiscalTx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Jahresbeleg TSE signing failed for register {RegisterId} year {Year}",
                request.CashRegisterId, year);
            return Fail(ex.Message);
        }

        var report = await _reportService.ComposeReportDtoAsync(
            jahresbeleg,
            summary,
            register.RegisterNumber,
            fiscalEnvironment,
            dailyClosing,
            cancellationToken);

        _logger.LogInformation(
            "Jahresbeleg created Id={JahresbelegId} Register={RegisterId} Year={Year} Simulated={Simulated} DecemberMb={DecemberMb}",
            jahresbeleg.Id,
            request.CashRegisterId,
            year,
            fiscalEnvironment.IsDemoFiscal,
            summary.IsDecemberMonatsbeleg);

        await _reportPdfCapture.TryCaptureClosingReportAsync(dailyClosing.Id, actorUserId, cancellationToken: cancellationToken);

        return new JahresbelegClosingResult
        {
            Success = true,
            JahresbelegId = jahresbeleg.Id,
            DailyClosingId = dailyClosing.Id,
            Year = year,
            TotalGross = summary.TotalGross,
            TotalTax = summary.TotalTax,
            TransactionCount = summary.TransactionCount,
            MonatsbelegCount = summary.MonatsbelegCount,
            TseSignature = tseSignature,
            PreviousSignature = previousSignature,
            IsSimulated = fiscalEnvironment.IsDemoFiscal,
            Environment = fiscalEnvironment.EnvironmentName,
            IsDecemberMonatsbeleg = summary.IsDecemberMonatsbeleg,
            Report = report,
        };
    }

    public async Task<JahresbelegDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Jahresbelege.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (entity == null)
            return null;

        var registerNumber = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.Id == entity.CashRegisterId)
            .Select(r => r.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var summary = JahresbelegReportComposer.BuildSummaryFromEntity(entity);
        var fiscalEnvironment = FiscalEnvironmentResolver.Resolve(
            _hostEnvironment,
            _tseOptions,
            _configuration,
            rksvEnvironment: _rksvEnvironment);

        return JahresbelegReportComposer.ToDetailDto(entity, summary, registerNumber, fiscalEnvironment);
    }

    public async Task<IReadOnlyList<JahresbelegListItemDto>> ListAsync(
        Guid? cashRegisterId,
        int? year,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Jahresbelege.AsNoTracking().AsQueryable();
        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
            query = query.Where(j => j.CashRegisterId == cashRegisterId.Value);
        if (year.HasValue)
            query = query.Where(j => j.Year == year.Value);

        var rows = await query
            .OrderByDescending(j => j.Year)
            .ThenByDescending(j => j.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        var regIds = rows.Select(r => r.CashRegisterId).Distinct().ToList();
        var regs = await _context.CashRegisters.AsNoTracking()
            .Where(r => regIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.RegisterNumber, cancellationToken);

        return rows.Select(r => new JahresbelegListItemDto
        {
            Id = r.Id,
            CashRegisterId = r.CashRegisterId,
            RegisterNumber = regs.GetValueOrDefault(r.CashRegisterId),
            Year = r.Year,
            TotalGross = r.TotalGross,
            TransactionCount = r.TransactionCount,
            IsSimulated = r.IsSimulated,
            Environment = r.Environment,
            IsDecemberMonatsbeleg = r.IsDecemberMonatsbeleg,
            CreatedAtUtc = r.CreatedAtUtc,
        }).ToList();
    }

    public async Task<PosDailyClosingReportDto?> BuildReportDtoAsync(
        Guid jahresbelegId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Jahresbelege.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jahresbelegId, cancellationToken);
        if (entity == null)
            return null;

        var registerNumber = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.Id == entity.CashRegisterId)
            .Select(r => r.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var summary = JahresbelegReportComposer.BuildSummaryFromEntity(entity);
        var fiscalEnvironment = FiscalEnvironmentResolver.Resolve(
            _hostEnvironment,
            _tseOptions,
            _configuration,
            rksvEnvironment: _rksvEnvironment);

        DailyClosing? linkedDailyClosing = null;
        if (entity.DailyClosingId.HasValue)
        {
            linkedDailyClosing = await _context.DailyClosings.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == entity.DailyClosingId.Value, cancellationToken);
        }

        return await _reportService.ComposeReportDtoAsync(
            entity,
            summary,
            registerNumber,
            fiscalEnvironment,
            linkedDailyClosing,
            cancellationToken);
    }

    public async Task<bool> CanCreateForCurrentYearAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var reg = await _context.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId, cancellationToken);
        if (reg == null || reg.Status == RegisterStatus.Decommissioned)
            return false;

        var year = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth().Year;
        var exists = await _context.Jahresbelege.AsNoTracking()
            .AnyAsync(j => j.CashRegisterId == cashRegisterId && j.Year == year, cancellationToken);
        return !exists;
    }

    private async Task<bool> GetDecemberMonatsbelegCountsAsJahresbelegAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var flag = await _context.CompanySettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => (bool?)s.UseDecemberMonatsbelegAsJahresbeleg)
            .FirstOrDefaultAsync(cancellationToken);
        return flag ?? true;
    }

    private async Task<string?> ResolvePreviousYearSignatureAsync(
        Guid cashRegisterId,
        int year,
        CancellationToken cancellationToken) =>
        await _context.Jahresbelege.AsNoTracking()
            .Where(j => j.CashRegisterId == cashRegisterId && j.Year == year - 1)
            .Select(j => j.TseSignature)
            .FirstOrDefaultAsync(cancellationToken);

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

    private static int ResolveTargetYear(int? year) =>
        year ?? PostgreSqlUtcDateTime.GetViennaCurrentYearMonth().Year;

    private static void ValidateYear(int year)
    {
        if (year < 2000 || year > 2100)
            throw new ArgumentOutOfRangeException(nameof(year), "Year out of supported range.");
    }

    private static JahresbelegClosingResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };

    private static bool IsDuplicateClosing(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("ix_jahresbeleg_per_register_year", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("ix_daily_closings", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;
}
