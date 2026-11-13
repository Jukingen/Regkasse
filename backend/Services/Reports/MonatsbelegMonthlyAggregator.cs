using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Reports;

/// <summary>Builds monthly RKSV closing totals from completed daily closings and payment-row snapshots.</summary>
public static class MonatsbelegMonthlyAggregator
{
    public static async Task<MonatsbelegSummaryDto> AggregateAsync(
        IDailyClosingService dailyClosingService,
        IQueryable<DailyClosing> dailyClosingQuery,
        Guid tenantId,
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var monthStart = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(year, month, 1);
        var monthEndDay = DateTime.DaysInMonth(year, month);
        var monthEnd = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(year, month, monthEndDay);
        var monthStartPersist = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(monthStart);
        var monthEndPersist = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(monthEnd);

        var dailyRows = await dailyClosingQuery
            .AsNoTracking()
            .Where(c =>
                c.CashRegisterId == cashRegisterId
                && c.ClosingType == "Daily"
                && c.Status == "Completed"
                && c.ClosingDate >= monthStartPersist
                && c.ClosingDate <= monthEndPersist)
            .OrderBy(c => c.ClosingDate)
            .ToListAsync(cancellationToken);

        decimal totalCash = 0m;
        decimal totalCard = 0m;
        decimal totalVoucher = 0m;
        decimal totalOther = 0m;
        var taxAccumulator = new Dictionary<int, decimal>();
        var txBreakdown = new TransactionBreakdown();

        for (var day = 1; day <= monthEndDay; day++)
        {
            var businessDate = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(year, month, day);
            var daySummary = await dailyClosingService.GenerateClosingSummaryAsync(
                tenantId,
                cashRegisterId,
                businessDate,
                cancellationToken);

            totalCash += daySummary.TotalCash;
            totalCard += daySummary.TotalCard;
            totalVoucher += daySummary.TotalVoucherRedemptions;
            totalOther += daySummary.TotalOtherPaymentMethods;

            txBreakdown.Cash += daySummary.TransactionBreakdown.Cash;
            txBreakdown.Card += daySummary.TransactionBreakdown.Card;
            txBreakdown.Voucher += daySummary.TransactionBreakdown.Voucher;
            txBreakdown.Cancellations += daySummary.TransactionBreakdown.Cancellations;
            txBreakdown.Total += daySummary.TransactionBreakdown.Total;

            MergeTaxBucket(taxAccumulator, TaxTypes.Standard, daySummary.TaxBreakdown.TaxAt20);
            MergeTaxBucket(taxAccumulator, TaxTypes.Reduced, daySummary.TaxBreakdown.TaxAt10);
            MergeTaxBucket(taxAccumulator, TaxTypes.ZeroRate, daySummary.TaxBreakdown.GrossAt0);
        }

        var fiscalGross = dailyRows.Sum(c => c.TotalAmount);
        var fiscalTax = dailyRows.Sum(c => c.TotalTaxAmount);
        var fiscalTxCount = dailyRows.Sum(c => c.TransactionCount);

        var taxBreakdown = MapTaxBreakdown(taxAccumulator);

        return new MonatsbelegSummaryDto
        {
            Year = year,
            Month = month,
            CashRegisterId = cashRegisterId,
            DailyClosingCount = dailyRows.Count,
            TotalCash = totalCash,
            TotalCard = totalCard,
            TotalVoucher = totalVoucher,
            TotalOther = totalOther,
            TotalGross = fiscalGross,
            TotalTax = fiscalTax,
            TaxRate20 = taxBreakdown.TaxAt20,
            TaxRate10 = taxBreakdown.TaxAt10,
            TaxRate0 = taxBreakdown.GrossAt0,
            TransactionCount = fiscalTxCount,
            PaymentBreakdown = PaymentBreakdown.FromAmounts(totalCash, totalCard, totalVoucher, totalOther),
            TaxBreakdown = taxBreakdown,
            TransactionBreakdown = txBreakdown,
        };
    }

    private static void MergeTaxBucket(Dictionary<int, decimal> acc, int taxType, decimal amount)
    {
        if (amount == 0m)
            return;
        acc[taxType] = acc.GetValueOrDefault(taxType) + amount;
    }

    private static DailyClosingTaxBreakdownDto MapTaxBreakdown(Dictionary<int, decimal> taxByType)
    {
        decimal Gross(int taxType, decimal taxAmount)
        {
            if (taxAmount == 0m)
                return 0m;
            var rate = TaxTypes.GetTaxRate(taxType);
            if (rate <= 0m)
                return Math.Round(taxAmount, 2, MidpointRounding.AwayFromZero);
            return Math.Round(taxAmount * (100m + rate) / rate, 2, MidpointRounding.AwayFromZero);
        }

        var tax20 = taxByType.GetValueOrDefault(TaxTypes.Standard);
        var tax10 = taxByType.GetValueOrDefault(TaxTypes.Reduced);
        var tax0 = taxByType.GetValueOrDefault(TaxTypes.ZeroRate);

        return new DailyClosingTaxBreakdownDto
        {
            TaxAt20 = tax20,
            GrossAt20 = Gross(TaxTypes.Standard, tax20),
            TaxAt10 = tax10,
            GrossAt10 = Gross(TaxTypes.Reduced, tax10),
            GrossAt0 = tax0,
        };
    }
}
