using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services
{
    public interface ITagesabschlussService
    {
        Task<TagesabschlussResult> PerformDailyClosingAsync(string userId, Guid cashRegisterId);
        Task<TagesabschlussResult> PerformMonthlyClosingAsync(string userId, Guid cashRegisterId);
        Task<TagesabschlussResult> PerformYearlyClosingAsync(string userId, Guid cashRegisterId);
        Task<List<TagesabschlussResult>> GetClosingHistoryAsync(string userId, DateTime? fromDate = null, DateTime? toDate = null);
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
            var registerNumber = await _context.CashRegisters
                .AsNoTracking()
                .Where(cr => cr.Id == cashRegisterId)
                .Select(cr => cr.RegisterNumber)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(registerNumber))
                return 0;

            return await _context.PaymentDetails
                .AsNoTracking()
                .Where(p => p.CreatedAt >= fromInclusive && p.CreatedAt < toExclusive
                    && p.IsActive
                    && p.KassenId == registerNumber
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

                var today = DateTime.Today;

                // Sprint 4: Block closing when payment-without-invoice exists (reconciliation enforcement)
                var paymentsWithoutInvoiceCount = await GetPaymentsWithoutInvoiceCountAsync(cashRegisterId, today, today.AddDays(1));
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
                               i.CreatedAt.Date == today &&
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

                // Create TSE signature for daily closing
                var tseSignature = await _tseService.CreateDailyClosingSignatureAsync(
                    cashRegisterId, 
                    today, 
                    totalAmount, 
                    transactionCount);

                // Create daily closing record
                var dailyClosing = new DailyClosing
                {
                    Id = Guid.NewGuid(),
                    CashRegisterId = cashRegisterId,
                    UserId = userId,
                    ClosingDate = today,
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
                    ClosingDate = today,
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
                var currentMonth = DateTime.Today.AddDays(-DateTime.Today.Day + 1);
                var periodEnd = DateTime.Today.AddDays(1);

                // Sprint 4: Block when payment-without-invoice exists in period
                var paymentsWithoutInvoiceCount = await GetPaymentsWithoutInvoiceCountAsync(cashRegisterId, currentMonth, periodEnd);
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
                               i.CreatedAt >= currentMonth &&
                               i.Status == InvoiceStatus.Paid)
                    .ToListAsync();

                if (!transactions.Any())
                {
                    throw new InvalidOperationException("No transactions found for current month.");
                }

                var totalAmount = transactions.Sum(t => t.TotalAmount);
                var totalTaxAmount = transactions.Sum(t => t.TaxAmount);
                var transactionCount = transactions.Count;

                var tseSignature = await _tseService.CreateMonthlyClosingSignatureAsync(
                    cashRegisterId, 
                    currentMonth, 
                    totalAmount, 
                    transactionCount);

                var monthlyClosing = new DailyClosing
                {
                    Id = Guid.NewGuid(),
                    CashRegisterId = cashRegisterId,
                    UserId = userId,
                    ClosingDate = currentMonth,
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
                    ClosingDate = currentMonth,
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
                var currentYear = new DateTime(DateTime.Today.Year, 1, 1);
                var periodEnd = DateTime.Today.AddDays(1);

                // Sprint 4: Block when payment-without-invoice exists in period
                var paymentsWithoutInvoiceCount = await GetPaymentsWithoutInvoiceCountAsync(cashRegisterId, currentYear, periodEnd);
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
                               i.CreatedAt >= currentYear &&
                               i.Status == InvoiceStatus.Paid)
                    .ToListAsync();

                if (!transactions.Any())
                {
                    throw new InvalidOperationException("No transactions found for current year.");
                }

                var totalAmount = transactions.Sum(t => t.TotalAmount);
                var totalTaxAmount = transactions.Sum(t => t.TaxAmount);
                var transactionCount = transactions.Count;

                var tseSignature = await _tseService.CreateYearlyClosingSignatureAsync(
                    cashRegisterId, 
                    currentYear, 
                    totalAmount, 
                    transactionCount);

                var yearlyClosing = new DailyClosing
                {
                    Id = Guid.NewGuid(),
                    CashRegisterId = cashRegisterId,
                    UserId = userId,
                    ClosingDate = currentYear,
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
                    ClosingDate = currentYear,
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

        public async Task<List<TagesabschlussResult>> GetClosingHistoryAsync(string userId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.DailyClosings
                .Where(d => d.UserId == userId);

            if (fromDate.HasValue)
                query = query.Where(d => d.ClosingDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(d => d.ClosingDate <= toDate.Value);

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
            var today = DateTime.Today;
            if (lastClosing.HasValue && lastClosing.Value.Date >= today)
                return false;

            // Sprint 4: Cannot close if payment-without-invoice exists (reconciliation block)
            var paymentsWithoutInvoiceCount = await GetPaymentsWithoutInvoiceCountAsync(cashRegisterId, today, today.AddDays(1));
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
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid? ClosingId { get; set; }
        public DateTime ClosingDate { get; set; }
        public string? ClosingType { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public int TransactionCount { get; set; }
        public string? TseSignature { get; set; }
        public string? Status { get; set; }
        public string? FinanzOnlineStatus { get; set; }
        /// <summary>When Success is false due to Sprint 4 enforcement: count of payments without Invoice that blocked closing. On success, 0.</summary>
        public int PaymentsWithoutInvoiceCount { get; set; }
        /// <summary>Optional warning on success. Unused when closing is blocked (PaymentsWithoutInvoiceCount &gt; 0).</summary>
        public string? Warning { get; set; }
    }
}
