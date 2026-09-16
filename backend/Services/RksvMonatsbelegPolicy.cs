using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <inheritdoc />
public sealed class RksvMonatsbelegPolicy : IRksvMonatsbelegPolicy
{
    private readonly AppDbContext _db;
    private readonly TseOptions _tseOptions;
    private readonly TimeProvider _timeProvider;

    public RksvMonatsbelegPolicy(
        AppDbContext db,
        IOptions<TseOptions> tseOptions,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _tseOptions = tseOptions.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public bool SessionGateApplies => !_tseOptions.IsOff && !_tseOptions.UseSoftTseWhenNoDevice;

    /// <inheritdoc />
    public async Task<bool> HasMonatsbelegForRegisterMonthAsync(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        if (month == 12)
        {
            var tenantId = await _db.CashRegisters.AsNoTracking()
                .Where(r => r.Id == cashRegisterId)
                .Select(r => r.TenantId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var decemberCountsAsJahresbeleg = await _db.CompanySettings.AsNoTracking()
                .Where(s => s.TenantId == tenantId)
                .Select(s => (bool?)s.UseDecemberMonatsbelegAsJahresbeleg)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false) ?? true;

            if (decemberCountsAsJahresbeleg)
            {
                // December: Jahresbeleg for the year satisfies the monthly gate (RKSV annual close).
                return await _db.PaymentDetails.AsNoTracking()
                    .AnyAsync(
                        p => p.CashRegisterId == cashRegisterId &&
                             p.IsActive &&
                             (
                                 (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg &&
                                  p.RksvSpecialReceiptYear == year) ||
                                 (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg &&
                                  p.RksvSpecialReceiptYear == year &&
                                  p.RksvSpecialReceiptMonth == 12)
                             ),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return await _db.PaymentDetails.AsNoTracking()
                .AnyAsync(
                    p => p.CashRegisterId == cashRegisterId &&
                         p.IsActive &&
                         p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg &&
                         p.RksvSpecialReceiptYear == year &&
                         p.RksvSpecialReceiptMonth == 12,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await _db.PaymentDetails.AsNoTracking()
            .AnyAsync(
                p => p.CashRegisterId == cashRegisterId &&
                     p.IsActive &&
                     p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg &&
                     p.RksvSpecialReceiptYear == year &&
                     p.RksvSpecialReceiptMonth == month,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MonatsbelegSalesGateDecision> EvaluateSalesGateAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var viennaNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var (prevYear, prevMonth) = PreviousViennaYearMonth(viennaNow);

        var previousMonthMissing = !await HasMonatsbelegForRegisterMonthAsync(
                cashRegisterId, prevYear, prevMonth, cancellationToken)
            .ConfigureAwait(false);

        var mode = await ResolveBlockingModeAsync(cashRegisterId, cancellationToken).ConfigureAwait(false);
        return MonatsbelegSalesGateEvaluator.Evaluate(
            SessionGateApplies,
            previousMonthMissing,
            mode,
            viennaNow.Day);
    }

    private async Task<MonatsbelegBlockingMode> ResolveBlockingModeAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var tenantId = await _db.CashRegisters.AsNoTracking()
            .Where(r => r.Id == cashRegisterId)
            .Select(r => (Guid?)r.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tenantId is null || tenantId == Guid.Empty)
            return MonatsbelegBlockingMode.Strict;

        var stored = await _db.CompanySettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId.Value)
            .Select(s => s.MonatsbelegBlockingMode)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return MonatsbelegBlockingModeNames.Parse(stored);
    }

    private static (int Year, int Month) PreviousViennaYearMonth(DateTime viennaLocalNow)
    {
        var anchor = new DateTime(viennaLocalNow.Year, viennaLocalNow.Month, 1).AddMonths(-1);
        return (anchor.Year, anchor.Month);
    }
}
