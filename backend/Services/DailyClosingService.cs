using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IDailyClosingService
{
    /// <summary>
    /// Builds a read-only payment-row snapshot for one Austria business day and tenant scope.
    /// </summary>
    /// <param name="tenantId">Effective tenant (admin resolver).</param>
    /// <param name="cashRegisterId">Optional register filter; when null, all registers of the tenant.</param>
    /// <param name="businessDate">Calendar date (year/month/day); interpreted as Europe/Vienna day boundary.</param>
    Task<DailyClosingSummaryDto> GenerateClosingSummaryAsync(
        Guid tenantId,
        Guid? cashRegisterId,
        DateTime businessDate,
        CancellationToken cancellationToken = default);
}

public sealed class DailyClosingService : IDailyClosingService
{
    private readonly AppDbContext _context;

    public DailyClosingService(AppDbContext context)
    {
        _context = context;
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

        var registerQuery = _context.CashRegisters.AsNoTracking().Where(cr => cr.TenantId == tenantId);
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

        var payments = await _context.PaymentDetails.AsNoTracking()
            .Where(p => registerIds.Contains(p.CashRegisterId)
                        && p.CreatedAt >= fromUtc
                        && p.CreatedAt < toExclusive
                        && p.IsActive)
            .ToListAsync(cancellationToken);

        var saleLike = payments
            .Where(p => !p.IsRefund && !p.IsStorno && p.RksvSpecialReceiptKind == null)
            .ToList();

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

        return new DailyClosingSummaryDto
        {
            BusinessDate = day,
            CashRegisterId = cashRegisterId,
            TotalSales = saleLike.Sum(p => p.TotalAmount),
            TotalCash = SumForMethod(saleLike, PaymentMethod.Cash),
            TotalCard = SumForMethod(saleLike, PaymentMethod.Card),
            TotalVoucherRedemptions = SumForMethod(saleLike, PaymentMethod.Voucher),
            TotalOtherPaymentMethods = saleLike
                .Where(p =>
                {
                    if (!int.TryParse(p.PaymentMethodRaw, out var v) || !Enum.IsDefined(typeof(PaymentMethod), v))
                        return true;
                    var m = (PaymentMethod)v;
                    return m is not (PaymentMethod.Cash or PaymentMethod.Card or PaymentMethod.Voucher);
                })
                .Sum(p => p.TotalAmount),
            ReceiptCount = saleLike.Count,
            StornoRowCount = stornoRows.Count,
            StornoTotalAmount = payments.Where(p => p.IsStorno).Sum(p => p.TotalAmount),
            SpecialReceipts = special,
            Stornos = stornoRows,
        };
    }

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
