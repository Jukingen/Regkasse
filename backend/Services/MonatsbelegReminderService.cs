using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <inheritdoc />
public sealed class MonatsbelegReminderService : IMonatsbelegReminderService
{
    private readonly AppDbContext _db;
    private readonly ISettingsTenantResolver _tenantResolver;

    public MonatsbelegReminderService(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver)
    {
        _db = db;
        _tenantResolver = tenantResolver;
    }

    /// <inheritdoc />
    public async Task<MonatsbelegStatusDto?> GetMonatsbelegStatusAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId && r.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (register == null)
            return null;

        var viennaNowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var (viennaYear, viennaMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var today = viennaNowLocal.Date;
        var currentMonthAnchor = new DateTime(viennaYear, viennaMonth, 1);
        var missingMonths = await GetMissingMonthsAsync(cashRegisterId, tenantId, today, currentMonthAnchor, cancellationToken)
            .ConfigureAwait(false);
        var missingMonthKeys = new HashSet<(int Year, int Month)>(missingMonths.Select(m => (m.Year, m.Month)));

        DateTime? lastCompletedMonthAnchor = null;
        DateTime? nextRequiredMonthAnchor = missingMonths.Count > 0
            ? new DateTime(missingMonths[0].Year, missingMonths[0].Month, 1)
            : null;

        var firstRequiredMonthAnchor = await GetFirstRequiredMonthAnchorAsync(cashRegisterId, register.CreatedAt, cancellationToken)
            .ConfigureAwait(false);
        var lastRequiredMonthAnchor = currentMonthAnchor.AddMonths(-1);
        for (var cursor = firstRequiredMonthAnchor; cursor <= lastRequiredMonthAnchor; cursor = cursor.AddMonths(1))
        {
            if (!missingMonthKeys.Contains((cursor.Year, cursor.Month)))
                lastCompletedMonthAnchor = cursor;
        }

        var requiresAttention = missingMonths.Count > 0;

        return new MonatsbelegStatusDto
        {
            LastCompletedMonth = lastCompletedMonthAnchor.HasValue ? FormatYearMonth(lastCompletedMonthAnchor.Value) : null,
            NextRequiredMonth = nextRequiredMonthAnchor.HasValue ? FormatYearMonth(nextRequiredMonthAnchor.Value) : null,
            MissingMonths = missingMonths,
            RequiresAttention = requiresAttention,
            TotalMissingCount = missingMonths.Count
        };
    }

    public List<MissingMonth> GetMissingMonths(Guid cashRegisterId)
        => GetMissingMonthsAsync(cashRegisterId, CancellationToken.None).GetAwaiter().GetResult();

    private async Task<List<MissingMonth>> GetMissingMonthsAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var viennaNowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var (viennaYear, viennaMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var today = viennaNowLocal.Date;
        var currentMonthAnchor = new DateTime(viennaYear, viennaMonth, 1);
        return await GetMissingMonthsAsync(cashRegisterId, tenantId, today, currentMonthAnchor, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<List<MissingMonth>> GetMissingMonthsAsync(
        Guid cashRegisterId,
        Guid tenantId,
        DateTime todayViennaDate,
        DateTime currentMonthAnchor,
        CancellationToken cancellationToken)
    {
        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId && r.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (register == null)
            return [];

        var firstRequiredMonthAnchor = await GetFirstRequiredMonthAnchorAsync(cashRegisterId, register.CreatedAt, cancellationToken)
            .ConfigureAwait(false);
        var lastRequiredMonthAnchor = currentMonthAnchor.AddMonths(-1);
        if (firstRequiredMonthAnchor > lastRequiredMonthAnchor)
            return [];

        var specialReceiptRows = await _db.PaymentDetails.AsNoTracking()
            .Where(p =>
                p.CashRegisterId == cashRegisterId &&
                p.IsActive &&
                p.RksvSpecialReceiptYear.HasValue &&
                (
                    p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg ||
                    p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg
                ))
            .Select(p => new
            {
                p.RksvSpecialReceiptKind,
                Year = p.RksvSpecialReceiptYear!.Value,
                Month = p.RksvSpecialReceiptMonth
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var completedMonths = new HashSet<(int Year, int Month)>();
        foreach (var row in specialReceiptRows)
        {
            if (row.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg && row.Month.HasValue)
            {
                completedMonths.Add((row.Year, row.Month.Value));
            }
            else if (row.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg)
            {
                completedMonths.Add((row.Year, 12));
            }
        }

        var missingMonths = new List<MissingMonth>();
        for (var cursor = firstRequiredMonthAnchor; cursor <= lastRequiredMonthAnchor; cursor = cursor.AddMonths(1))
        {
            if (completedMonths.Contains((cursor.Year, cursor.Month)))
                continue;

            var deadline = cursor.AddMonths(2).AddDays(-1);
            missingMonths.Add(new MissingMonth
            {
                Year = cursor.Year,
                Month = cursor.Month,
                IsOverdue = todayViennaDate > deadline.Date,
                Deadline = DateOnly.FromDateTime(deadline)
            });
        }

        return missingMonths;
    }

    private async Task<DateTime> GetFirstRequiredMonthAnchorAsync(
        Guid cashRegisterId,
        DateTime registerCreatedAtUtc,
        CancellationToken cancellationToken)
    {
        var firstFiscalOperationUtc = await _db.PaymentDetails.AsNoTracking()
            .Where(p => p.CashRegisterId == cashRegisterId && p.IsActive)
            .OrderBy(p => p.CreatedAt)
            .Select(p => (DateTime?)p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var basisUtc = firstFiscalOperationUtc ?? registerCreatedAtUtc;
        var basisUtcNormalized = basisUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(basisUtc, DateTimeKind.Utc)
            : basisUtc.ToUniversalTime();
        var basisVienna = TimeZoneInfo.ConvertTimeFromUtc(basisUtcNormalized, PostgreSqlUtcDateTime.AustriaTimeZone);
        return new DateTime(basisVienna.Year, basisVienna.Month, 1);
    }

    private static string FormatYearMonth(DateTime monthAnchor)
        => $"{monthAnchor.Year:D4}-{monthAnchor.Month:D2}";
}
