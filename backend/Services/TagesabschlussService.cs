using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;

namespace KasseAPI_Final.Services
{
    public interface ITagesabschlussService
    {
        Task<TagesabschlussResult> PerformDailyClosingAsync(string userId, Guid cashRegisterId);
        Task<TagesabschlussResult> PerformMonthlyClosingAsync(string userId, Guid cashRegisterId);
        Task<TagesabschlussResult> PerformYearlyClosingAsync(string userId, Guid cashRegisterId);
        /// <param name="cashRegisterId">When set, restricts history to closings for that register (still scoped to the authenticated user).</param>
        Task<List<TagesabschlussResult>> GetClosingHistoryAsync(string userId, DateTime? fromDate = null, DateTime? toDate = null, Guid? cashRegisterId = null);
        Task<bool> CanPerformClosingAsync(Guid cashRegisterId);
        Task<DateTime?> GetLastClosingDateAsync(Guid cashRegisterId);
        /// <summary>Sprint 4: Count active payments in scope with no Invoice (SourcePaymentId). Used for blocking and readiness.</summary>
        Task<int> GetPaymentsWithoutInvoiceCountAsync(Guid cashRegisterId, DateTime fromInclusive, DateTime toExclusive);
    }

    public class TagesabschlussService : ITagesabschlussService
    {
        private readonly AppDbContext _context;
        private readonly ITseService _tseService;
        private readonly IFinanzOnlineService _finanzOnlineService;

        public TagesabschlussService(
            AppDbContext context,
            ITseService tseService,
            IFinanzOnlineService finanzOnlineService)
        {
            _context = context;
            _tseService = tseService;
            _finanzOnlineService = finanzOnlineService;
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
                // Check if TSE is connected
                var tseStatus = await _tseService.GetTseStatusAsync();
                if (!tseStatus.IsConnected)
                {
                    throw new InvalidOperationException("TSE device is not connected. Daily closing cannot be performed.");
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

                await _context.SaveChangesAsync();

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
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow
                };

                _context.DailyClosings.Add(monthlyClosing);
                await _context.SaveChangesAsync();

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
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow
                };

                _context.DailyClosings.Add(yearlyClosing);
                await _context.SaveChangesAsync();

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

        public async Task<List<TagesabschlussResult>> GetClosingHistoryAsync(string userId, DateTime? fromDate = null, DateTime? toDate = null, Guid? cashRegisterId = null)
        {
            var query = _context.DailyClosings
                .Where(d => d.UserId == userId);

            if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
                query = query.Where(d => d.CashRegisterId == cashRegisterId.Value);

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
                .ToListAsync();

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

        public async Task<DateTime?> GetLastClosingDateAsync(Guid cashRegisterId)
        {
            var lastClosing = await _context.DailyClosings
                .Where(d => d.CashRegisterId == cashRegisterId && d.ClosingType == "Daily")
                .OrderByDescending(d => d.ClosingDate)
                .FirstOrDefaultAsync();

            return lastClosing?.ClosingDate;
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
