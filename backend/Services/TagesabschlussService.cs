using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services
{
    public interface ITagesabschlussService
    {
        Task<TagesabschlussResult> PerformDailyClosingAsync(string userId, Guid cashRegisterId);
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
        Task<bool> CanPerformClosingAsync(Guid cashRegisterId);
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

        public TagesabschlussService(
            AppDbContext context,
            ITseService tseService,
            ITseProvider tseProvider,
            ITseKeyProvider tseKeyProvider,
            IFinanzOnlineService finanzOnlineService,
            IOptions<TseOptions> tseOptions,
            IHostEnvironment hostEnvironment,
            ILogger<TagesabschlussService> logger,
            IDevelopmentModeService? developmentModeService = null)
        {
            _context = context;
            _tseService = tseService;
            _tseKeyProvider = tseKeyProvider;
            _tseProvider = tseProvider;
            _finanzOnlineService = finanzOnlineService;
            _tseOptions = tseOptions.Value;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
            _developmentModeService = developmentModeService;
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

        public async Task<TagesabschlussResult> PerformDailyClosingAsync(string userId, Guid cashRegisterId)
        {
            try
            {
                if (!await CanPerformClosingAsync(cashRegisterId))
                {
                    var viennaTodayBlocked = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
                    var (dayStartUtcBlocked, dayEndExclusiveUtcBlocked) =
                        PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaTodayBlocked);
                    var blockedPaymentsWithoutInvoiceCount =
                        await GetPaymentsWithoutInvoiceCountAsync(
                            cashRegisterId,
                            dayStartUtcBlocked,
                            dayEndExclusiveUtcBlocked);
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
                        ErrorMessage = "Daily closing already performed for today",
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

                var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
                var (dayStartUtc, dayEndExclusiveUtc) =
                    PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);

                // Sprint 4: Block closing when payment-without-invoice exists (reconciliation enforcement)
                var paymentsWithoutInvoiceCount =
                    await GetPaymentsWithoutInvoiceCountAsync(cashRegisterId, dayStartUtc, dayEndExclusiveUtc);
                if (paymentsWithoutInvoiceCount > 0)
                {
                    return new TagesabschlussResult
                    {
                        Success = false,
                        ErrorMessage = $"Closing blocked: {paymentsWithoutInvoiceCount} payment(s) without a matching invoice. Resolve gaps (e.g. run backfill) and try again.",
                        PaymentsWithoutInvoiceCount = paymentsWithoutInvoiceCount
                    };
                }

                // Get today's transactions (Invoice-authoritative; no Payment totals)
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
                    throw new InvalidOperationException("No transactions found for today. Cannot perform daily closing.");
                }

                // Calculate totals
                var totalAmount = transactions.Sum(t => t.TotalAmount);
                var totalTaxAmount = transactions.Sum(t => t.TaxAmount);
                var transactionCount = transactions.Count;

                var register = await _context.CashRegisters.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == cashRegisterId)
                    ?? throw new InvalidOperationException($"Cash register {cashRegisterId} not found.");

                var tseSignature = await _tseService.CreateDailyClosingSignatureAsync(
                    cashRegisterId,
                    register.RegisterNumber,
                    viennaToday,
                    totalAmount,
                    transactionCount);

                // Create daily closing record
                var dailyClosing = new DailyClosing
                {
                    Id = Guid.NewGuid(),
                    CashRegisterId = cashRegisterId,
                    UserId = userId,
                    ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(viennaToday),
                    ClosingType = "Daily",
                    TotalAmount = totalAmount,
                    TotalTaxAmount = totalTaxAmount,
                    TransactionCount = transactionCount,
                    TseSignature = tseSignature,
                    CertificateThumbprint = _tseKeyProvider.GetCurrentCertificateThumbprint(),
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow
                };

                _context.DailyClosings.Add(dailyClosing);

                // Submit to FinanzOnline if enabled
                if (await _finanzOnlineService.IsEnabledAsync())
                {
                    try
                    {
                        await _finanzOnlineService.SubmitDailyClosingAsync(dailyClosing);
                        dailyClosing.FinanzOnlineStatus = "Submitted";
                    }
                    catch (Exception ex)
                    {
                        dailyClosing.FinanzOnlineStatus = "Failed";
                        dailyClosing.FinanzOnlineError = ex.Message;
                    }
                }

                var duplicateResult = await TrySaveClosingOrReturnDuplicateAsync(dailyClosing.ClosingType);
                if (duplicateResult != null)
                    return duplicateResult;

                return new TagesabschlussResult
                {
                    Success = true,
                    ClosingId = dailyClosing.Id,
                    ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(viennaToday),
                    TotalAmount = totalAmount,
                    TotalTaxAmount = totalTaxAmount,
                    TransactionCount = transactionCount,
                    TseSignature = tseSignature,
                    FinanzOnlineStatus = dailyClosing.FinanzOnlineStatus,
                    PaymentsWithoutInvoiceCount = 0,
                    Warning = null
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

                var tseSignature = await _tseService.CreateMonthlyClosingSignatureAsync(
                    cashRegisterId,
                    registerM.RegisterNumber,
                    currentMonthLocal,
                    totalAmount,
                    transactionCount);

                var monthlyClosing = new DailyClosing
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

                _context.DailyClosings.Add(monthlyClosing);
                var duplicateResult = await TrySaveClosingOrReturnDuplicateAsync(monthlyClosing.ClosingType);
                if (duplicateResult != null)
                    return duplicateResult;

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

                var tseSignature = await _tseService.CreateYearlyClosingSignatureAsync(
                    cashRegisterId,
                    registerY.RegisterNumber,
                    currentYearLocal,
                    totalAmount,
                    transactionCount);

                var yearlyClosing = new DailyClosing
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

                _context.DailyClosings.Add(yearlyClosing);
                var duplicateResult = await TrySaveClosingOrReturnDuplicateAsync(yearlyClosing.ClosingType);
                if (duplicateResult != null)
                    return duplicateResult;

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

            return closings.Select(c => new TagesabschlussResult
            {
                Success = true,
                ClosingId = c.Id,
                ClosingDate = c.ClosingDate,
                ClosingType = c.ClosingType,
                TotalAmount = c.TotalAmount,
                TotalTaxAmount = c.TotalTaxAmount,
                TransactionCount = c.TransactionCount,
                TseSignature = c.TseSignature,
                Status = c.Status,
                FinanzOnlineStatus = c.FinanzOnlineStatus
            }).ToList();
        }

        public async Task<bool> CanPerformClosingAsync(Guid cashRegisterId)
        {
            var reg = await _context.CashRegisters.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == cashRegisterId)
                .ConfigureAwait(false);
            if (reg == null || reg.Status == RegisterStatus.Decommissioned)
                return false;

            var lastClosing = await GetLastClosingDateAsync(cashRegisterId);
            var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
            // ClosingDate is stored as UTC (Vienna midnight instant); compare via Vienna calendar, not .Date on UTC.
            if (lastClosing.HasValue)
            {
                var lastViennaDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(lastClosing.Value);
                if (lastViennaDay >= viennaToday)
                    return false;
            }

            var (dayStartUtc, dayEndExclusiveUtc) =
                PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);
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

        private async Task<TagesabschlussResult?> TrySaveClosingOrReturnDuplicateAsync(string closingType)
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
                return new TagesabschlussResult
                {
                    Success = false,
                    ErrorMessage = closingType switch
                    {
                        "Monthly" => "Monthly closing already performed for the current month",
                        "Yearly" => "Yearly closing already performed for the current year",
                        _ => "Daily closing already performed for today",
                    },
                };
            }
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
    }
}
