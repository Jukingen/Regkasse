using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <inheritdoc />
public sealed class RksvReminderService : IRksvReminderService
{
    private const string MbOk = "ok";
    private const string MbUpcoming = "upcoming";
    private const string MbOverdue = "overdue";

    private const string SbMissing = "missing";
    private const string SbPresent = "present";

    private readonly AppDbContext _db;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IRksvMonatsbelegPolicy _monatsbelegPolicy;

    public RksvReminderService(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        IRksvMonatsbelegPolicy monatsbelegPolicy)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _monatsbelegPolicy = monatsbelegPolicy;
    }

    /// <inheritdoc />
    public async Task<RksvReminderStatusDto?> GetRksvStatusAsync(Guid cashRegisterId, CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId && r.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (register == null)
            return null;

        var settings = await _db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        var decemberMbAsJahresbeleg = settings?.UseDecemberMonatsbelegAsJahresbeleg ?? true;

        var viennaLocalNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var (viennaYear, viennaMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var today = viennaLocalNow.Date;

        var hasStartbelegMarker = register.StartbelegCreatedAt.HasValue;
        var startbeleg = new RksvReminderStartbelegDto
        {
            IsRequired = !hasStartbelegMarker,
            Status = hasStartbelegMarker ? SbPresent : SbMissing,
        };

        var lastDayCurrent = DateTime.DaysInMonth(viennaYear, viennaMonth);
        var endOfCurrentMonth = new DateTime(viennaYear, viennaMonth, lastDayCurrent);
        var daysUntilMonthEnd = (endOfCurrentMonth - today).Days;

        var prevMonthAnchor = new DateTime(viennaYear, viennaMonth, 1).AddMonths(-1);
        var prevYear = prevMonthAnchor.Year;
        var prevMonth = prevMonthAnchor.Month;

        var hasCurrentMonth = await _monatsbelegPolicy
            .HasMonatsbelegForRegisterMonthAsync(cashRegisterId, viennaYear, viennaMonth, cancellationToken)
            .ConfigureAwait(false);
        var hasPreviousMonth = await _monatsbelegPolicy
            .HasMonatsbelegForRegisterMonthAsync(cashRegisterId, prevYear, prevMonth, cancellationToken)
            .ConfigureAwait(false);

        var mbRequired = !hasCurrentMonth || !hasPreviousMonth;

        var currentMonthGraceOverdue = !hasCurrentMonth && today.Day > 7;
        var lastMonthMissing = !hasPreviousMonth;

        string mbStatus;
        if (!hasPreviousMonth || currentMonthGraceOverdue || (!hasCurrentMonth && daysUntilMonthEnd <= 1))
            mbStatus = MbOverdue;
        else if (hasCurrentMonth && hasPreviousMonth)
            mbStatus = MbOk;
        else
            mbStatus = MbUpcoming;

        int? mbDays = mbRequired ? daysUntilMonthEnd : null;

        var warningMessageDe = BuildMonatsbelegReminderWarningDe(lastMonthMissing, currentMonthGraceOverdue);

        var monatsbeleg = new RksvReminderMonatsbelegDto
        {
            IsRequired = mbRequired,
            DaysUntilDeadline = mbDays,
            Status = mbStatus,
            CurrentMonthExists = hasCurrentMonth,
            LastMonthExists = hasPreviousMonth,
            CurrentMonthOverdue = currentMonthGraceOverdue,
            LastMonthMissing = lastMonthMissing,
            WarningMessageDe = warningMessageDe,
        };

        var hasJbPriorYear = await HasJahresbelegForViennaYearAsync(
                cashRegisterId, viennaYear - 1, decemberMbAsJahresbeleg, cancellationToken)
            .ConfigureAwait(false);
        var hasJbCurrentYear = await HasJahresbelegForViennaYearAsync(
                cashRegisterId, viennaYear, decemberMbAsJahresbeleg, cancellationToken)
            .ConfigureAwait(false);

        var endDecPriorYear = new DateTime(viennaYear - 1, 12, 31);
        var pastEndDecPriorYear = today > endDecPriorYear;

        var jbRequiredPrior = !hasJbPriorYear && pastEndDecPriorYear;
        var jbRequiredDecember = !hasJbCurrentYear && viennaMonth == 12;

        var jbRequired = jbRequiredPrior || jbRequiredDecember;

        var lastDayDecVy = new DateTime(viennaYear, 12, 31);
        var daysUntilDec31Vy = (lastDayDecVy - today).Days;

        int? jbDays = null;
        if (jbRequiredDecember && viennaMonth == 12 && !hasJbCurrentYear)
            jbDays = Math.Max(0, daysUntilDec31Vy);

        string jbStatus;
        if (!jbRequired)
            jbStatus = MbOk;
        else if (jbRequiredPrior || (jbRequiredDecember && daysUntilDec31Vy <= 1))
            jbStatus = MbOverdue;
        else
            jbStatus = MbUpcoming;

        var jahresbeleg = new RksvReminderJahresbelegDto
        {
            IsRequired = jbRequired,
            DaysUntilDeadline = jbDays,
            Status = jbStatus,
        };

        return new RksvReminderStatusDto
        {
            Startbeleg = startbeleg,
            Monatsbeleg = monatsbeleg,
            Jahresbeleg = jahresbeleg,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RksvReminderRegisterStatusItemDto>> GetRksvStatusOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var registerIds = await _db.CashRegisters.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Status != RegisterStatus.Decommissioned)
            .OrderBy(r => r.RegisterNumber)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new List<RksvReminderRegisterStatusItemDto>(registerIds.Count);
        foreach (var registerId in registerIds)
        {
            var status = await GetRksvStatusAsync(registerId, cancellationToken).ConfigureAwait(false);
            if (status == null)
                continue;
            results.Add(new RksvReminderRegisterStatusItemDto
            {
                CashRegisterId = registerId,
                Status = status,
            });
        }

        return results;
    }

    private async Task<bool> HasJahresbelegForViennaYearAsync(
        Guid cashRegisterId,
        int year,
        bool decemberMonatsbelegCountsAsJahresbeleg,
        CancellationToken cancellationToken)
    {
        if (decemberMonatsbelegCountsAsJahresbeleg)
        {
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
                     p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg &&
                     p.RksvSpecialReceiptYear == year,
                cancellationToken)
                .ConfigureAwait(false);
    }

    private static string? BuildMonatsbelegReminderWarningDe(bool lastMonthMissing, bool currentMonthGraceOverdue)
    {
        if (lastMonthMissing)
            return "Monatsbeleg für den Vormonat fehlt. Bitte umgehend erstellen.";
        if (currentMonthGraceOverdue)
            return "Monatsbeleg für aktuellen Monat überfällig! Bitte erstellen Sie den Monatsbeleg sofort.";
        return null;
    }
}
