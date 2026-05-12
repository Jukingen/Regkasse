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
    private readonly TimeProvider _timeProvider;
    private readonly IRksvMonatsbelegPolicy _monatsbelegPolicy;

    public MonatsbelegReminderService(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        TimeProvider timeProvider,
        IRksvMonatsbelegPolicy monatsbelegPolicy)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _timeProvider = timeProvider;
        _monatsbelegPolicy = monatsbelegPolicy;
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

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var viennaNowLocal = TimeZoneInfo.ConvertTimeFromUtc(utcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var viennaYear = viennaNowLocal.Year;
        var viennaMonth = viennaNowLocal.Month;
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

        var daysUntilDeadline = 0;
        if (missingMonths.Count > 0)
        {
            var deadlineDate = missingMonths[0].Deadline.ToDateTime(TimeOnly.MinValue);
            daysUntilDeadline = (int)(deadlineDate - today).TotalDays;
            if (daysUntilDeadline < 0)
                daysUntilDeadline = 0;
        }

        var anyOverdue = missingMonths.Any(m => m.IsOverdue);
        var warningLevel = anyOverdue
            ? "red"
            : requiresAttention && viennaNowLocal.Day > 7
                ? "yellow"
                : "none";

        var prevViennaMonthAnchor = currentMonthAnchor.AddMonths(-1);
        var hasCurrentMonthMb = await _monatsbelegPolicy
            .HasMonatsbelegForRegisterMonthAsync(cashRegisterId, viennaYear, viennaMonth, cancellationToken)
            .ConfigureAwait(false);
        var hasLastMonthMb = await _monatsbelegPolicy
            .HasMonatsbelegForRegisterMonthAsync(
                cashRegisterId, prevViennaMonthAnchor.Year, prevViennaMonthAnchor.Month, cancellationToken)
            .ConfigureAwait(false);

        var currentMonthInComplianceWindow = currentMonthAnchor >= firstRequiredMonthAnchor;
        var lastMonthInComplianceWindow = prevViennaMonthAnchor >= firstRequiredMonthAnchor;
        var currentMonthOverdue = !hasCurrentMonthMb && viennaNowLocal.Day > 7 && currentMonthInComplianceWindow;
        var lastMonthMissing = !hasLastMonthMb && lastMonthInComplianceWindow;
        var warningMessage = BuildMonatsbelegWarningMessageDe(lastMonthMissing, currentMonthOverdue);

        var lastMbUtc = await _db.PaymentDetails.AsNoTracking()
            .Where(p =>
                p.CashRegisterId == cashRegisterId &&
                p.IsActive &&
                p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => (DateTime?)p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new MonatsbelegStatusDto
        {
            LastCompletedMonth = lastCompletedMonthAnchor.HasValue ? FormatYearMonth(lastCompletedMonthAnchor.Value) : null,
            NextRequiredMonth = nextRequiredMonthAnchor.HasValue ? FormatYearMonth(nextRequiredMonthAnchor.Value) : null,
            MissingMonths = missingMonths,
            RequiresAttention = requiresAttention,
            TotalMissingCount = missingMonths.Count,
            IsRequired = requiresAttention,
            DaysUntilDeadline = daysUntilDeadline,
            LastMonatsbelegDate = lastMbUtc.HasValue ? DateTime.SpecifyKind(lastMbUtc.Value, DateTimeKind.Utc).ToString("O") : null,
            WarningLevel = warningLevel,
            CurrentMonthExists = hasCurrentMonthMb,
            LastMonthExists = hasLastMonthMb,
            CurrentMonthOverdue = currentMonthOverdue,
            LastMonthMissing = lastMonthMissing,
            WarningMessage = warningMessage
        };
    }

    public List<MissingMonth> GetMissingMonths(Guid cashRegisterId)
        => GetMissingMonthsAsync(cashRegisterId, CancellationToken.None).GetAwaiter().GetResult();

    private async Task<List<MissingMonth>> GetMissingMonthsAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var viennaNowLocal = TimeZoneInfo.ConvertTimeFromUtc(utcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var today = viennaNowLocal.Date;
        var currentMonthAnchor = new DateTime(viennaNowLocal.Year, viennaNowLocal.Month, 1);
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

    /// <summary>German operator copy for POS/dashboard; prioritises missing previous month.</summary>
    private static string? BuildMonatsbelegWarningMessageDe(bool lastMonthMissing, bool currentMonthOverdue)
    {
        if (lastMonthMissing)
            return "Monatsbeleg für den Vormonat fehlt. Bitte umgehend erstellen.";
        if (currentMonthOverdue)
            return "Monatsbeleg für aktuellen Monat überfällig! Bitte erstellen Sie den Monatsbeleg sofort.";
        return null;
    }
}
