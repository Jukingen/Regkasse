using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KasseAPI_Final.Services
{
    public interface ITagesabschlussService
    {
        /// <param name="closingDate">
        /// Optional Vienna business day (date components). Null = today.
        /// Past dates create a late (nachträglich) closing; <see cref="DailyClosing.CreatedAt"/> is never backdated.
        /// </param>
        Task<TagesabschlussResult> PerformDailyClosingAsync(
            string userId,
            Guid cashRegisterId,
            DateTime? closingDate = null,
            string? reason = null);
        Task<TagesabschlussResult> PerformMonthlyClosingAsync(string userId, Guid cashRegisterId);
        Task<TagesabschlussResult> PerformYearlyClosingAsync(string userId, Guid cashRegisterId);
        /// <param name="cashRegisterId">Register whose closing rows are returned (tenant-scoped via EF filters).</param>
        Task<List<TagesabschlussResult>> GetClosingHistoryAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            Guid cashRegisterId = default,
            CancellationToken cancellationToken = default);
        /// <summary>
        /// Resolves the operational register for Manager FA: explicit id when valid, otherwise default/sole/first active register.
        /// </summary>
        Task<Guid?> ResolveOperationalCashRegisterIdAsync(
            Guid tenantId,
            Guid? cashRegisterId,
            CancellationToken cancellationToken = default);
        /// <param name="closingDate">Optional Vienna business day. Null = today. Future dates are not closeable.</param>
        Task<bool> CanPerformClosingAsync(Guid cashRegisterId, DateTime? closingDate = null);
        Task<bool> CanPerformMonthlyClosingAsync(Guid cashRegisterId);
        Task<bool> CanPerformYearlyClosingAsync(Guid cashRegisterId);
        Task<DateTime?> GetLastClosingDateAsync(Guid cashRegisterId);
        Task<DateTime?> GetLastClosingDateForTypeAsync(Guid cashRegisterId, string closingType);
        /// <summary>UTC instant when the latest completed closing of the given type was persisted (<see cref="DailyClosing.CreatedAt"/>).</summary>
        Task<DateTime?> GetLastClosingPerformedAtForTypeAsync(Guid cashRegisterId, string closingType);
        /// <summary>Sprint 4: Count active payments in scope with no Invoice (SourcePaymentId). Used for blocking and readiness.</summary>
        Task<int> GetPaymentsWithoutInvoiceCountAsync(Guid cashRegisterId, DateTime fromInclusive, DateTime toExclusive);
    }

    public class TagesabschlussService : ITagesabschlussService
    {
        private readonly AppDbContext _context;
        private readonly ITseService _tseService;
        private readonly ITseProvider _tseProvider;
        private readonly ITseKeyProvider _tseKeyProvider;
        private readonly IFinanzOnlineService _finanzOnlineService;
        private readonly TseOptions _tseOptions;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IDevelopmentModeService? _developmentModeService;
        private readonly ILogger<TagesabschlussService> _logger;
        private readonly IReportPdfCaptureService _reportPdfCapture;
        private readonly IReportPdfStorageService _reportPdfStorage;
        private readonly IAuditLogService? _auditLogService;
        private readonly ActivityEventRecorder? _activityEvents;

        public TagesabschlussService(
            AppDbContext context,
            ITseService tseService,
            ITseProvider tseProvider,
            ITseKeyProvider tseKeyProvider,
            IFinanzOnlineService finanzOnlineService,
            IOptions<TseOptions> tseOptions,
            IHostEnvironment hostEnvironment,
            ILogger<TagesabschlussService> logger,
            IReportPdfCaptureService reportPdfCapture,
            IReportPdfStorageService reportPdfStorage,
            IDevelopmentModeService? developmentModeService = null,
            IAuditLogService? auditLogService = null,
            ActivityEventRecorder? activityEvents = null)
        {
            _context = context;
            _tseService = tseService;
            _tseKeyProvider = tseKeyProvider;
            _tseProvider = tseProvider;
            _finanzOnlineService = finanzOnlineService;
            _tseOptions = tseOptions.Value;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
            _reportPdfCapture = reportPdfCapture;
            _reportPdfStorage = reportPdfStorage;
            _developmentModeService = developmentModeService;
            _auditLogService = auditLogService;
            _activityEvents = activityEvents;
        }

        /// <summary>
        /// Dev/demo bypass for daily closing when no hardware TSE is connected.
        /// Aligns with <see cref="TseService.GetDeviceStatusAsync"/> and payment TSE policy.
        /// </summary>
        private bool AllowDailyClosingWithoutConnectedTse()
        {
            if (_developmentModeService?.ShouldBypassTseCheck() == true)
                return true;

            if (_tseProvider is not FakeTseProvider)
                return false;

            return _tseOptions.AllowSimulatedDailyClosing
                || _tseOptions.IsFakeSigningMode
                || _tseOptions.UseSoftTseWhenNoDevice;
        }

        /// <summary>Sprint 4: Count active payments in scope (register + date range) that have no Invoice with SourcePaymentId. Used to block closing when &gt; 0.</summary>
        public async Task<int> GetPaymentsWithoutInvoiceCountAsync(Guid cashRegisterId, DateTime fromInclusive, DateTime toExclusive)
        {
            if (cashRegisterId == Guid.Empty)
                return 0;
            if (!await _context.CashRegisters.AsNoTracking().AnyAsync(cr => cr.Id == cashRegisterId))
                return 0;

            fromInclusive = PostgreSqlUtcDateTime.ToUtcForNpgsql(fromInclusive);
            toExclusive = PostgreSqlUtcDateTime.ToUtcForNpgsql(toExclusive);

            return await _context.PaymentDetails
                .AsNoTracking()
                .Where(p => p.CreatedAt >= fromInclusive && p.CreatedAt < toExclusive
                    && p.IsActive
                    && p.CashRegisterId == cashRegisterId
                    && !_context.Invoices.Any(i => i.SourcePaymentId == p.Id))
                .CountAsync();
        }

        public async Task<TagesabschlussResult> PerformDailyClosingAsync(
            string userId,
            Guid cashRegisterId,
            DateTime? closingDate = null,
            string? reason = null)
        {
            try
            {
                var resolve = TryResolveDailyClosingBusinessDay(closingDate);
                if (resolve.ErrorMessage != null)
                {
                    return new TagesabschlussResult
                    {
                        Success = false,
                        ErrorMessage = resolve.ErrorMessage,
                    };
                }

                var businessDay = resolve.BusinessDay;
                var isBackdated = resolve.IsBackdated;
                var lateReason = NormalizeLateCreationReason(reason);
                if (isBackdated && lateReason == null)
                {
                    return new TagesabschlussResult
                    {
                        Success = false,
                        ErrorMessage = "A reason is required for backdated (nachträglich) daily closings.",
                        IsBackdated = true,
                    };
                }

                var (dayStartUtc, dayEndExclusiveUtc) =
                    PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(businessDay);

                if (!await CanPerformClosingAsync(cashRegisterId, businessDay))
                {
                    var blockedPaymentsWithoutInvoiceCount =
                        await GetPaymentsWithoutInvoiceCountAsync(
                            cashRegisterId,
                            dayStartUtc,
                            dayEndExclusiveUtc);
                    if (blockedPaymentsWithoutInvoiceCount > 0)
                    {
                        return new TagesabschlussResult
                        {
                            Success = false,
                            ErrorMessage =
                                $"Closing blocked: {blockedPaymentsWithoutInvoiceCount} payment(s) without a matching invoice. Resolve gaps (e.g. run backfill) and try again.",
                            PaymentsWithoutInvoiceCount = blockedPaymentsWithoutInvoiceCount,
                        };
                    }

                    var blockedRegister = await _context.CashRegisters.AsNoTracking()
                        .FirstOrDefaultAsync(r => r.Id == cashRegisterId);
                    if (blockedRegister == null || blockedRegister.Status == RegisterStatus.Decommissioned)
                    {
                        return new TagesabschlussResult
                        {
                            Success = false,
                            ErrorMessage = "Cash register is not available for daily closing",
                        };
                    }

                    return new TagesabschlussResult
                    {
                        Success = false,
                        ErrorMessage = isBackdated
                            ? $"Daily closing already performed for {businessDay:yyyy-MM-dd}"
                            : "Daily closing already performed for today",
                        IsBackdated = isBackdated,
                    };
                }

                // Check if TSE is connected (dev/demo may bypass — see AllowDailyClosingWithoutConnectedTse).
                var tseStatus = await _tseService.GetTseStatusAsync();
                if (!tseStatus.IsConnected)
                {
                    if (!AllowDailyClosingWithoutConnectedTse())
                    {
                        throw new InvalidOperationException("TSE device is not connected. Daily closing cannot be performed.");
                    }

                    _logger.LogWarning(
                        "TSE device is not connected, but daily closing is allowed in dev/demo. Provider={ProviderType}, Mode={Mode}, TseMode={TseMode}, Environment={EnvironmentName}, DevBypassTse={DevBypassTse}",
                        _tseProvider.GetType().Name,
                        _tseOptions.Mode,
                        _tseOptions.TseMode,
                        _hostEnvironment.EnvironmentName,
                        _developmentModeService?.ShouldBypassTseCheck() == true);
                }

                // Sprint 4: Block closing when payment-without-invoice exists (reconciliation enforcement)
                var paymentsWithoutInvoiceCount =
                    await GetPaymentsWithoutInvoiceCountAsync(cashRegisterId, dayStartUtc, dayEndExclusiveUtc);
                if (paymentsWithoutInvoiceCount > 0)
                {
                    return new TagesabschlussResult
                    {
                        Success = false,
                        ErrorMessage = $"Closing blocked: {paymentsWithoutInvoiceCount} payment(s) without a matching invoice. Resolve gaps (e.g. run backfill) and try again.",
                        PaymentsWithoutInvoiceCount = paymentsWithoutInvoiceCount,
                        IsBackdated = isBackdated,
                    };
                }

                // Invoice-authoritative totals for the selected Vienna business day
                var transactions = await _context.Invoices
                    .Where(i => i.CashRegisterId == cashRegisterId &&
                               i.CreatedAt >= dayStartUtc &&
                               i.CreatedAt < dayEndExclusiveUtc &&
                               i.Status == InvoiceStatus.Paid)
                    .Where(i => i.SourcePaymentId == null ||
                                !_context.PaymentDetails.Any(p =>
                                    p.Id == i.SourcePaymentId!.Value &&
                                    p.RksvSpecialReceiptKind != null))
                    .ToListAsync();

                if (!transactions.Any())
                {
                    throw new InvalidOperationException(
                        isBackdated
                            ? $"No transactions found for {businessDay:yyyy-MM-dd}. Cannot perform daily closing."
                            : "No transactions found for today. Cannot perform daily closing.");
                }

                var totalAmount = transactions.Sum(t => t.TotalAmount);
                var totalTaxAmount = transactions.Sum(t => t.TaxAmount);
                var transactionCount = transactions.Count;

                var register = await _context.CashRegisters.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == cashRegisterId)
                    ?? throw new InvalidOperationException($"Cash register {cashRegisterId} not found.");

                await using var fiscalTx = await _context.Database.BeginTransactionAsync();
                string tseSignature;
                DailyClosing dailyClosing;
                try
                {
                    tseSignature = await _tseService.CreateDailyClosingSignatureAsync(
                        cashRegisterId,
                        register.RegisterNumber,
                        businessDay,
                        totalAmount,
                        transactionCount,
                        fiscalTx);

                    // ClosingDate = business day; CreatedAt = real UTC now (never backdated), same honesty as Monatsbeleg nachträglich.
                    var closingAnchorUtc = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(businessDay);
                    dailyClosing = new DailyClosing
                    {
                        Id = Guid.NewGuid(),
                        CashRegisterId = cashRegisterId,
                        UserId = userId,
                        ClosingDate = closingAnchorUtc,
                        IsBackdated = isBackdated,
                        LateCreationReason = isBackdated ? lateReason : null,
                        ClosingType = "Daily",
                        TotalAmount = totalAmount,
                        TotalTaxAmount = totalTaxAmount,
                        TransactionCount = transactionCount,
                        TseSignature = tseSignature,
                        CertificateThumbprint = _tseKeyProvider.GetCurrentCertificateThumbprint(),
                        Status = "Completed",
                        CreatedAt = DateTime.UtcNow
                    };

                    await DailyClosingOperationalResolver.StampOperationalFieldsAsync(
                        _context,
                        dailyClosing,
                        cashRegisterId,
                        userId);

                    _context.DailyClosings.Add(dailyClosing);

                    if (isBackdated)
                    {
                        _logger.LogInformation(
                            "Backdated (nachträglich) daily closing for CashRegisterId={CashRegisterId} BusinessDay={BusinessDay:yyyy-MM-dd} ActorUserId={UserId}; CreatedAt remains real UTC; ReasonLength={ReasonLength}",
                            cashRegisterId,
                            businessDay,
                            userId,
                            lateReason?.Length ?? 0);
                    }

                    // Submit to FinanzOnline if enabled (simulate path; status stamped before commit).
                    if (await _finanzOnlineService.IsEnabledAsync())
                    {
                        try
                        {
                            var fon = await _finanzOnlineService.SubmitDailyClosingAsync(dailyClosing);
                            dailyClosing.FinanzOnlineStatus = fon.Success
                                ? (string.IsNullOrWhiteSpace(fon.Status) ? "Submitted" : fon.Status)
                                : "Failed";
                            if (!fon.Success)
                                dailyClosing.FinanzOnlineError = fon.ErrorMessage;
                        }
                        catch (Exception ex)
                        {
                            dailyClosing.FinanzOnlineStatus = "Failed";
                            dailyClosing.FinanzOnlineError = ex.Message;
                        }
                    }

                    var duplicateResult = await TrySaveClosingOrReturnDuplicateAsync(
                        dailyClosing.ClosingType,
                        isBackdated,
                        businessDay);
                    if (duplicateResult != null)
                    {
                        await fiscalTx.RollbackAsync();
                        return duplicateResult;
                    }

                    await fiscalTx.CommitAsync();
                }
                catch
                {
                    await fiscalTx.RollbackAsync();
                    throw;
                }

                await TryAuditDailyClosingCreatedAsync(
                    userId,
                    cashRegisterId,
                    dailyClosing.Id,
                    dailyClosing.TenantId,
                    businessDay,
                    isBackdated,
                    lateReason,
                    dailyClosing.CreatedAt);

                await _reportPdfCapture.TryCaptureClosingReportAsync(dailyClosing.Id, userId);

                return new TagesabschlussResult
                {
                    Success = true,
                    ClosingId = dailyClosing.Id,
                    ClosingDate = dailyClosing.ClosingDate,
                    ClosingType = "Daily",
                    TotalAmount = totalAmount,
                    TotalTaxAmount = totalTaxAmount,
                    TransactionCount = transactionCount,
                    TseSignature = tseSignature,
                    FinanzOnlineStatus = dailyClosing.FinanzOnlineStatus,
                    PaymentsWithoutInvoiceCount = 0,
                    IsBackdated = isBackdated,
                    LateCreationReason = isBackdated ? lateReason : null,
                    CreatedAt = dailyClosing.CreatedAt,
                    Warning = isBackdated
                        ? $"Backdated daily closing for {businessDay:yyyy-MM-dd}; creation timestamp is the real current UTC time (nachträglich, audit-transparent)."
                        : null
                };
            }
            catch (Exception ex)
            {
                return new TagesabschlussResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<TagesabschlussResult> PerformMonthlyClosingAsync(string userId, Guid cashRegisterId)
        {
            try
            {
                if (!await CanPerformMonthlyClosingAsync(cashRegisterId))
                {
                    var viennaTodayBlocked = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
                    var currentMonthLocalBlocked = new DateTime(
                        viennaTodayBlocked.Year,
                        viennaTodayBlocked.Month,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Unspecified);
                    var (monthStartUtcBlocked, _) =
                        PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(currentMonthLocalBlocked);
                    var (_, periodEndUtcBlocked) =
                        PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaTodayBlocked);
                    var blockedPaymentsWithoutInvoiceCount =
                        await GetPaymentsWithoutInvoiceCountAsync(
                            cashRegisterId,
                            monthStartUtcBlocked,
                            periodEndUtcBlocked);
                    if (blockedPaymentsWithoutInvoiceCount > 0)
                    {
                        return new TagesabschlussResult
                        {
                            Success = false,
                            ErrorMessage =
                                $"Closing blocked: {blockedPaymentsWithoutInvoiceCount} payment(s) without a matching invoice in the period. Resolve gaps (e.g. run backfill) and try again.",
                            PaymentsWithoutInvoiceCount = blockedPaymentsWithoutInvoiceCount,
                        };
                    }

                    var blockedRegister = await _context.CashRegisters.AsNoTracking()
                        .FirstOrDefaultAsync(r => r.Id == cashRegisterId);
                    if (blockedRegister == null || blockedRegister.Status == RegisterStatus.Decommissioned)
                    {
                        return new TagesabschlussResult
                        {
                            Success = false,
                            ErrorMessage = "Cash register is not available for monthly closing",
                        };
                    }

                    return new TagesabschlussResult
                    {
                        Success = false,
                        ErrorMessage = "Monthly closing already performed for the current month",
                    };
                }

                var viennaTodayM = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
                var currentMonthLocal = new DateTime(viennaTodayM.Year, viennaTodayM.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
                var (monthStartUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(currentMonthLocal);
                var (_, periodEndUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaTodayM);

                // Sprint 4: Block when payment-without-invoice exists in period
                var paymentsWithoutInvoiceCount =
                    await GetPaymentsWithoutInvoiceCountAsync(cashRegisterId, monthStartUtc, periodEndUtc);
                if (paymentsWithoutInvoiceCount > 0)
                {
                    return new TagesabschlussResult
                    {
                        Success = false,
                        ErrorMessage = $"Closing blocked: {paymentsWithoutInvoiceCount} payment(s) without a matching invoice in the period. Resolve gaps (e.g. run backfill) and try again.",
                        PaymentsWithoutInvoiceCount = paymentsWithoutInvoiceCount
                    };
                }

                var transactions = await _context.Invoices
                    .Where(i => i.CashRegisterId == cashRegisterId &&
                               i.CreatedAt >= monthStartUtc &&
                               i.Status == InvoiceStatus.Paid)
                    .Where(i => i.SourcePaymentId == null ||
                                !_context.PaymentDetails.Any(p =>
                                    p.Id == i.SourcePaymentId!.Value &&
                                    p.RksvSpecialReceiptKind != null))
                    .ToListAsync();

                if (!transactions.Any())
                {
                    throw new InvalidOperationException("No transactions found for current month.");
                }

                var totalAmount = transactions.Sum(t => t.TotalAmount);
                var totalTaxAmount = transactions.Sum(t => t.TaxAmount);
                var transactionCount = transactions.Count;

                var registerM = await _context.CashRegisters.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == cashRegisterId)
                    ?? throw new InvalidOperationException($"Cash register {cashRegisterId} not found.");

                await using var fiscalTx = await _context.Database.BeginTransactionAsync();
                string tseSignature;
                DailyClosing monthlyClosing;
                try
                {
                    tseSignature = await _tseService.CreateMonthlyClosingSignatureAsync(
                        cashRegisterId,
                        registerM.RegisterNumber,
                        currentMonthLocal,
                        totalAmount,
                        transactionCount,
                        fiscalTx);

                    monthlyClosing = new DailyClosing
                    {
                        Id = Guid.NewGuid(),
                        CashRegisterId = cashRegisterId,
                        UserId = userId,
                        ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(currentMonthLocal),
                        ClosingType = "Monthly",
                        TotalAmount = totalAmount,
                        TotalTaxAmount = totalTaxAmount,
                        TransactionCount = transactionCount,
                        TseSignature = tseSignature,
                        CertificateThumbprint = _tseKeyProvider.GetCurrentCertificateThumbprint(),
                        Status = "Completed",
                        CreatedAt = DateTime.UtcNow
                    };

                    await DailyClosingOperationalResolver.StampOperationalFieldsAsync(
                        _context,
                        monthlyClosing,
                        cashRegisterId,
                        userId);

                    _context.DailyClosings.Add(monthlyClosing);
                    var duplicateResult = await TrySaveClosingOrReturnDuplicateAsync(monthlyClosing.ClosingType);
                    if (duplicateResult != null)
                    {
                        await fiscalTx.RollbackAsync();
                        return duplicateResult;
                    }

                    await fiscalTx.CommitAsync();
                }
                catch
                {
                    await fiscalTx.RollbackAsync();
                    throw;
                }

                await _reportPdfCapture.TryCaptureClosingReportAsync(monthlyClosing.Id, userId);

                return new TagesabschlussResult
                {
                    Success = true,
                    ClosingId = monthlyClosing.Id,
                    ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(currentMonthLocal),
                    TotalAmount = totalAmount,
                    TotalTaxAmount = totalTaxAmount,
                    TransactionCount = transactionCount,
                    TseSignature = tseSignature
                };
            }
            catch (Exception ex)
            {
                return new TagesabschlussResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<TagesabschlussResult> PerformYearlyClosingAsync(string userId, Guid cashRegisterId)
        {
            try
            {
                if (!await CanPerformYearlyClosingAsync(cashRegisterId))
                {
                    var viennaTodayBlocked = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
                    var currentYearLocalBlocked = new DateTime(
                        viennaTodayBlocked.Year,
                        1,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Unspecified);
                    var (yearStartUtcBlocked, _) =
                        PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(currentYearLocalBlocked);
                    var (_, yearPeriodEndUtcBlocked) =
                        PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaTodayBlocked);
                    var blockedPaymentsWithoutInvoiceCount =
                        await GetPaymentsWithoutInvoiceCountAsync(
                            cashRegisterId,
                            yearStartUtcBlocked,
                            yearPeriodEndUtcBlocked);
                    if (blockedPaymentsWithoutInvoiceCount > 0)
                    {
                        return new TagesabschlussResult
                        {
                            Success = false,
                            ErrorMessage =
                                $"Closing blocked: {blockedPaymentsWithoutInvoiceCount} payment(s) without a matching invoice in the period. Resolve gaps (e.g. run backfill) and try again.",
                            PaymentsWithoutInvoiceCount = blockedPaymentsWithoutInvoiceCount,
                        };
                    }

                    var blockedRegister = await _context.CashRegisters.AsNoTracking()
                        .FirstOrDefaultAsync(r => r.Id == cashRegisterId);
                    if (blockedRegister == null || blockedRegister.Status == RegisterStatus.Decommissioned)
                    {
                        return new TagesabschlussResult
                        {
                            Success = false,
                            ErrorMessage = "Cash register is not available for yearly closing",
                        };
                    }

                    return new TagesabschlussResult
                    {
                        Success = false,
                        ErrorMessage = "Yearly closing already performed for the current year",
                    };
                }

                var viennaTodayY = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
                var currentYearLocal = new DateTime(viennaTodayY.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
                var (yearStartUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(currentYearLocal);
                var (_, yearPeriodEndUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaTodayY);

                // Sprint 4: Block when payment-without-invoice exists in period
                var paymentsWithoutInvoiceCount =
                    await GetPaymentsWithoutInvoiceCountAsync(cashRegisterId, yearStartUtc, yearPeriodEndUtc);
                if (paymentsWithoutInvoiceCount > 0)
                {
                    return new TagesabschlussResult
                    {
                        Success = false,
                        ErrorMessage = $"Closing blocked: {paymentsWithoutInvoiceCount} payment(s) without a matching invoice in the period. Resolve gaps (e.g. run backfill) and try again.",
                        PaymentsWithoutInvoiceCount = paymentsWithoutInvoiceCount
                    };
                }

                var transactions = await _context.Invoices
                    .Where(i => i.CashRegisterId == cashRegisterId &&
                               i.CreatedAt >= yearStartUtc &&
                               i.Status == InvoiceStatus.Paid)
                    .Where(i => i.SourcePaymentId == null ||
                                !_context.PaymentDetails.Any(p =>
                                    p.Id == i.SourcePaymentId!.Value &&
                                    p.RksvSpecialReceiptKind != null))
                    .ToListAsync();

                if (!transactions.Any())
                {
                    throw new InvalidOperationException("No transactions found for current year.");
                }

                var totalAmount = transactions.Sum(t => t.TotalAmount);
                var totalTaxAmount = transactions.Sum(t => t.TaxAmount);
                var transactionCount = transactions.Count;

                var registerY = await _context.CashRegisters.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == cashRegisterId)
                    ?? throw new InvalidOperationException($"Cash register {cashRegisterId} not found.");

                await using var fiscalTx = await _context.Database.BeginTransactionAsync();
                string tseSignature;
                DailyClosing yearlyClosing;
                try
                {
                    tseSignature = await _tseService.CreateYearlyClosingSignatureAsync(
                        cashRegisterId,
                        registerY.RegisterNumber,
                        currentYearLocal,
                        totalAmount,
                        transactionCount,
                        fiscalTx);

                    yearlyClosing = new DailyClosing
                    {
                        Id = Guid.NewGuid(),
                        CashRegisterId = cashRegisterId,
                        UserId = userId,
                        ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(currentYearLocal),
                        ClosingType = "Yearly",
                        TotalAmount = totalAmount,
                        TotalTaxAmount = totalTaxAmount,
                        TransactionCount = transactionCount,
                        TseSignature = tseSignature,
                        CertificateThumbprint = _tseKeyProvider.GetCurrentCertificateThumbprint(),
                        Status = "Completed",
                        CreatedAt = DateTime.UtcNow
                    };

                    await DailyClosingOperationalResolver.StampOperationalFieldsAsync(
                        _context,
                        yearlyClosing,
                        cashRegisterId,
                        userId);

                    _context.DailyClosings.Add(yearlyClosing);
                    var duplicateResult = await TrySaveClosingOrReturnDuplicateAsync(yearlyClosing.ClosingType);
                    if (duplicateResult != null)
                    {
                        await fiscalTx.RollbackAsync();
                        return duplicateResult;
                    }

                    await fiscalTx.CommitAsync();
                }
                catch
                {
                    await fiscalTx.RollbackAsync();
                    throw;
                }

                await _reportPdfCapture.TryCaptureClosingReportAsync(yearlyClosing.Id, userId);

                return new TagesabschlussResult
                {
                    Success = true,
                    ClosingId = yearlyClosing.Id,
                    ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(currentYearLocal),
                    TotalAmount = totalAmount,
                    TotalTaxAmount = totalTaxAmount,
                    TransactionCount = transactionCount,
                    TseSignature = tseSignature
                };
            }
            catch (Exception ex)
            {
                return new TagesabschlussResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<Guid?> ResolveOperationalCashRegisterIdAsync(
            Guid tenantId,
            Guid? cashRegisterId,
            CancellationToken cancellationToken = default)
        {
            if (tenantId == Guid.Empty)
                return null;

            if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
            {
                var exists = await _context.CashRegisters.AsNoTracking()
                    .AnyAsync(
                        r => r.Id == cashRegisterId.Value && r.TenantId == tenantId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return exists ? cashRegisterId.Value : null;
            }

            var registers = await _context.CashRegisters.AsNoTracking()
                .Where(r => r.TenantId == tenantId && r.Status != RegisterStatus.Decommissioned)
                .OrderByDescending(r => r.IsDefaultForTenant)
                .ThenBy(r => r.RegisterNumber)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return registers.FirstOrDefault()?.Id;
        }

        public async Task<List<TagesabschlussResult>> GetClosingHistoryAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            Guid cashRegisterId = default,
            CancellationToken cancellationToken = default)
        {
            if (cashRegisterId == Guid.Empty)
                return new List<TagesabschlussResult>();

            var query = _context.DailyClosings
                .Where(d => d.CashRegisterId == cashRegisterId);

            // ClosingDate rows are discrete Vienna-midnight anchors (one instant per business day), not arbitrary instants.
            // Inclusive calendar filter: lower bound = start of from-day; upper bound = start of to-day (equals that day's stored anchor).
            if (fromDate.HasValue)
            {
                var fromUtc = PostgreSqlUtcDateTime.ToUtcForNpgsql(fromDate.Value);
                query = query.Where(d => d.ClosingDate >= fromUtc);
            }

            if (toDate.HasValue)
            {
                var toUtc = PostgreSqlUtcDateTime.ToUtcForNpgsql(toDate.Value);
                query = query.Where(d => d.ClosingDate <= toUtc);
            }

            var closings = await query
                .OrderByDescending(d => d.ClosingDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var storedByType = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal);
            foreach (var group in closings.GroupBy(c => ReportPdfTypes.FromClosingType(c.ClosingType)))
            {
                var ids = group.Select(c => c.Id).ToList();
                var stored = await _reportPdfStorage.GetStoredReportIdsAsync(
                    group.Key,
                    ids,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                storedByType[group.Key] = stored;
            }

            return closings.Select(c =>
            {
                var reportType = ReportPdfTypes.FromClosingType(c.ClosingType);
                var hasStoredPdf = storedByType.TryGetValue(reportType, out var stored)
                                   && stored is not null
                                   && stored.Contains(c.Id);
                return new TagesabschlussResult
                {
                    Success = true,
                    ClosingId = c.Id,
                    ClosingDate = c.ClosingDate,
                    CreatedAt = c.CreatedAt,
                    ClosingType = c.ClosingType,
                    TotalAmount = c.TotalAmount,
                    TotalTaxAmount = c.TotalTaxAmount,
                    TransactionCount = c.TransactionCount,
                    TseSignature = c.TseSignature,
                    Status = c.Status,
                    FinanzOnlineStatus = c.FinanzOnlineStatus,
                    HasStoredPdf = hasStoredPdf,
                    IsBackdated = c.IsBackdated || IsLateCreatedDailyClosing(c),
                    LateCreationReason = c.LateCreationReason,
                };
            }).ToList();
        }

        public async Task<bool> CanPerformClosingAsync(Guid cashRegisterId, DateTime? closingDate = null)
        {
            var resolve = TryResolveDailyClosingBusinessDay(closingDate);
            if (resolve.ErrorMessage != null)
                return false;

            var businessDay = resolve.BusinessDay;

            var reg = await _context.CashRegisters.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == cashRegisterId)
                .ConfigureAwait(false);
            if (reg == null || reg.Status == RegisterStatus.Decommissioned)
                return false;

            if (await HasDailyClosingForBusinessDayAsync(cashRegisterId, businessDay).ConfigureAwait(false))
                return false;

            var (dayStartUtc, dayEndExclusiveUtc) =
                PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(businessDay);
            // Sprint 4: Cannot close if payment-without-invoice exists (reconciliation block)
            var paymentsWithoutInvoiceCount =
                await GetPaymentsWithoutInvoiceCountAsync(cashRegisterId, dayStartUtc, dayEndExclusiveUtc);
            return paymentsWithoutInvoiceCount == 0;
        }

        public async Task<bool> CanPerformMonthlyClosingAsync(Guid cashRegisterId)
        {
            var reg = await _context.CashRegisters.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == cashRegisterId)
                .ConfigureAwait(false);
            if (reg == null || reg.Status == RegisterStatus.Decommissioned)
                return false;

            var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
            var currentMonthLocal = new DateTime(
                viennaToday.Year,
                viennaToday.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Unspecified);
            var lastMonthly = await GetLastClosingDateForTypeAsync(cashRegisterId, "Monthly");
            if (lastMonthly.HasValue)
            {
                var lastMonthAnchor =
                    PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(lastMonthly.Value);
                if (lastMonthAnchor.Year == currentMonthLocal.Year &&
                    lastMonthAnchor.Month == currentMonthLocal.Month)
                    return false;
            }

            var (monthStartUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(currentMonthLocal);
            var (_, periodEndUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);
            var paymentsWithoutInvoiceCount =
                await GetPaymentsWithoutInvoiceCountAsync(cashRegisterId, monthStartUtc, periodEndUtc);
            return paymentsWithoutInvoiceCount == 0;
        }

        public async Task<bool> CanPerformYearlyClosingAsync(Guid cashRegisterId)
        {
            var reg = await _context.CashRegisters.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == cashRegisterId)
                .ConfigureAwait(false);
            if (reg == null || reg.Status == RegisterStatus.Decommissioned)
                return false;

            var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
            var currentYearLocal = new DateTime(viennaToday.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var lastYearly = await GetLastClosingDateForTypeAsync(cashRegisterId, "Yearly");
            if (lastYearly.HasValue)
            {
                var lastYearAnchor =
                    PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(lastYearly.Value);
                if (lastYearAnchor.Year == currentYearLocal.Year)
                    return false;
            }

            var (yearStartUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(currentYearLocal);
            var (_, yearPeriodEndUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);
            var paymentsWithoutInvoiceCount =
                await GetPaymentsWithoutInvoiceCountAsync(cashRegisterId, yearStartUtc, yearPeriodEndUtc);
            return paymentsWithoutInvoiceCount == 0;
        }

        public async Task<DateTime?> GetLastClosingDateAsync(Guid cashRegisterId) =>
            await GetLastClosingDateForTypeAsync(cashRegisterId, "Daily");

        public async Task<DateTime?> GetLastClosingDateForTypeAsync(Guid cashRegisterId, string closingType)
        {
            var lastClosing = await GetLatestCompletedClosingForTypeAsync(cashRegisterId, closingType);
            return lastClosing?.ClosingDate;
        }

        public async Task<DateTime?> GetLastClosingPerformedAtForTypeAsync(Guid cashRegisterId, string closingType)
        {
            var lastClosing = await GetLatestCompletedClosingForTypeAsync(cashRegisterId, closingType);
            return lastClosing?.CreatedAt;
        }

        private async Task<DailyClosing?> GetLatestCompletedClosingForTypeAsync(Guid cashRegisterId, string closingType)
        {
            return await _context.DailyClosings
                .Where(d =>
                    d.CashRegisterId == cashRegisterId
                    && d.ClosingType == closingType
                    && d.Status == "Completed")
                .OrderByDescending(d => d.ClosingDate)
                .ThenByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();
        }

        private async Task<TagesabschlussResult?> TrySaveClosingOrReturnDuplicateAsync(
            string closingType,
            bool isBackdated = false,
            DateTime? businessDay = null)
        {
            try
            {
                await _context.SaveChangesAsync();
                return null;
            }
            catch (DbUpdateException ex) when (IsClosingPeriodDuplicate(ex))
            {
                _logger.LogWarning(
                    "Duplicate RKSV closing blocked by unique index (type={ClosingType})",
                    closingType);
                var dailyMsg = isBackdated && businessDay.HasValue
                    ? $"Daily closing already performed for {businessDay.Value:yyyy-MM-dd}"
                    : "Daily closing already performed for today";
                return new TagesabschlussResult
                {
                    Success = false,
                    ErrorMessage = closingType switch
                    {
                        "Monthly" => "Monthly closing already performed for the current month",
                        "Yearly" => "Yearly closing already performed for the current year",
                        _ => dailyMsg,
                    },
                    IsBackdated = isBackdated,
                };
            }
        }

        /// <summary>
        /// Resolves a Vienna calendar midnight for daily closing. Future days are rejected.
        /// </summary>
        private static (DateTime BusinessDay, bool IsBackdated, string? ErrorMessage) TryResolveDailyClosingBusinessDay(
            DateTime? closingDate)
        {
            var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
            if (!closingDate.HasValue)
                return (viennaToday, false, null);

            var businessDay = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(
                closingDate.Value.Year,
                closingDate.Value.Month,
                closingDate.Value.Day);

            if (businessDay > viennaToday)
            {
                return (businessDay, false, "Daily closing cannot be performed for a future date");
            }

            return (businessDay, businessDay < viennaToday, null);
        }

        private async Task<bool> HasDailyClosingForBusinessDayAsync(Guid cashRegisterId, DateTime businessDayLocal)
        {
            var closingAnchorUtc = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(businessDayLocal);
            return await _context.DailyClosings.AsNoTracking()
                .AnyAsync(d =>
                    d.CashRegisterId == cashRegisterId
                    && d.ClosingType == "Daily"
                    && d.ClosingDate == closingAnchorUtc)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// True when the closing was persisted on a later Vienna calendar day than <see cref="DailyClosing.ClosingDate"/>
        /// (nachträglich / late creation — real CreatedAt, business-day ClosingDate).
        /// </summary>
        private static bool IsLateCreatedDailyClosing(DailyClosing closing)
        {
            if (!string.Equals(closing.ClosingType, "Daily", StringComparison.OrdinalIgnoreCase))
                return false;

            var businessDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(closing.ClosingDate);
            var createdDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(closing.CreatedAt);
            return createdDay > businessDay;
        }

        private async Task TryAuditDailyClosingCreatedAsync(
            string userId,
            Guid cashRegisterId,
            Guid closingId,
            Guid tenantId,
            DateTime businessDay,
            bool isBackdated,
            string? lateReason,
            DateTime createdAtUtc)
        {
            if (_auditLogService == null)
                return;

            try
            {
                var action = isBackdated ? "TagesabschlussBackdatedCreated" : "TagesabschlussCreated";
                var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
                var daysLate = isBackdated
                    ? Math.Max(0, (viennaToday.Date - businessDay.Date).Days)
                    : 0;
                var description = isBackdated
                    ? $"Nachträglicher Tagesabschluss für {businessDay:yyyy-MM-dd} erstellt (CreatedAt = echte UTC-Zeit, DaysLate={daysLate})"
                    : $"Tagesabschluss für {businessDay:yyyy-MM-dd} erstellt";

                await _auditLogService.LogSystemOperationAsync(
                    action,
                    "DailyClosing",
                    userId,
                    "Unknown",
                    description: description,
                    requestData: new
                    {
                        cashRegisterId,
                        userId,
                        closingDate = businessDay.ToString("yyyy-MM-dd"),
                        isBackdated,
                        backdatedReason = lateReason,
                        reason = lateReason,
                        createdAt = createdAtUtc,
                        daysLate,
                    },
                    responseData: new { closingId, isBackdated, daysLate },
                    entityId: closingId,
                    tenantId: tenantId == Guid.Empty ? null : tenantId);

                if (isBackdated && _activityEvents != null && tenantId != Guid.Empty)
                {
                    await _activityEvents.TryPublishAsync(
                        new ActivityEventPublishRequest(
                            tenantId,
                            ActivityEventType.DailyClosingBackdatedCreated,
                            "Nachträglicher Tagesabschluss erstellt",
                            Description: description,
                            DedupKey: $"daily_closing_backdated:{closingId:N}",
                            ActorUserId: userId,
                            EntityType: "DailyClosing",
                            EntityId: closingId.ToString("N")),
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to write audit log for Tagesabschluss {ClosingId} (backdated={IsBackdated})",
                    closingId,
                    isBackdated);
            }
        }

        /// <summary>Trimmed reason for late daily closings; null when empty. Max 500 chars.</summary>
        private static string? NormalizeLateCreationReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return null;
            var trimmed = reason.Trim();
            return trimmed.Length <= 500 ? trimmed : trimmed[..500];
        }

        private static bool IsClosingPeriodDuplicate(DbUpdateException ex)
        {
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                if (inner is PostgresException pg &&
                    pg.SqlState == PostgresErrorCodes.UniqueViolation &&
                    pg.ConstraintName?.Contains("DailyClosings", StringComparison.OrdinalIgnoreCase) == true)
                    return true;
            }

            return false;
        }
    }

    public class TagesabschlussResult
    {
        [Required]
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid? ClosingId { get; set; }
        [Required]
        public DateTime ClosingDate { get; set; }

        /// <summary>Real UTC creation/signing instant (never forged for late closings).</summary>
        [Required]
        public DateTime CreatedAt { get; set; }

        public string? ClosingType { get; set; }
        [Required]
        public decimal TotalAmount { get; set; }
        [Required]
        public decimal TotalTaxAmount { get; set; }
        [Required]
        public int TransactionCount { get; set; }
        public string? TseSignature { get; set; }
        public string? Status { get; set; }
        public string? FinanzOnlineStatus { get; set; }
        /// <summary>When Success is false due to Sprint 4 enforcement: count of payments without Invoice that blocked closing. On success, 0.</summary>
        [Required]
        public int PaymentsWithoutInvoiceCount { get; set; }
        /// <summary>Optional warning on success. Unused when closing is blocked (PaymentsWithoutInvoiceCount &gt; 0).</summary>
        public string? Warning { get; set; }
        /// <summary>True when a persisted RKSV closing PDF exists for download.</summary>
        public bool HasStoredPdf { get; set; }
        /// <summary>
        /// True when this daily closing covers a past Vienna business day (nachträglich).
        /// Creation/signing timestamps remain real UTC — not backdated.
        /// </summary>
        [Required]
        public bool IsBackdated { get; set; }

        /// <summary>Operator reason when <see cref="IsBackdated"/>; null for on-time closings.</summary>
        public string? LateCreationReason { get; set; }
    }
}
