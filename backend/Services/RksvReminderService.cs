using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
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

        var prevMonthAnchor = new DateTime(viennaYear, viennaMonth, 1).AddMonths(-1);
        var prevYear = prevMonthAnchor.Year;
        var prevMonth = prevMonthAnchor.Month;

        var hasCurrentMonth = await _monatsbelegPolicy
            .HasMonatsbelegForRegisterMonthAsync(cashRegisterId, viennaYear, viennaMonth, cancellationToken)
            .ConfigureAwait(false);
        var hasPreviousMonth = await _monatsbelegPolicy
            .HasMonatsbelegForRegisterMonthAsync(cashRegisterId, prevYear, prevMonth, cancellationToken)
            .ConfigureAwait(false);

        var gate = await _monatsbelegPolicy
            .EvaluateSalesGateAsync(cashRegisterId, cancellationToken)
            .ConfigureAwait(false);

        // RKSV: Monatsbeleg is due for the previous completed month (within 7 days of month end).
        var mbRequired = !hasPreviousMonth;
        var currentMonthGraceOverdue = false;
        var lastMonthMissing = !hasPreviousMonth;

        string mbStatus;
        if (!hasPreviousMonth && gate.WarningLevel == MonatsbelegSalesGateEvaluator.WarningRed)
            mbStatus = MbOverdue;
        else if (!hasPreviousMonth)
            mbStatus = MbUpcoming;
        else
            mbStatus = MbOk;

        int? mbDays = mbRequired ? Math.Max(0, 7 - today.Day) : null;

        var warningMessageDe = gate.WarningMessageDe
            ?? BuildMonatsbelegReminderWarningDe(lastMonthMissing, currentMonthGraceOverdue);

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
            BlockingMode = MonatsbelegBlockingModeNames.ToPersisted(gate.Mode),
            WarningLevel = gate.WarningLevel,
            SalesBlocked = gate.BlocksSales,
            CanContinueWithWarning = gate.CanContinueWithWarning,
            ViennaDayOfMonth = gate.ViennaDayOfMonth,
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

        var (fonRequired, fonReminderStatus, fonDays, fonSubmissionStatus) = await ResolveJahresbelegFonAsync(
                cashRegisterId,
                viennaYear - 1,
                hasJbPriorYear,
                today,
                cancellationToken)
            .ConfigureAwait(false);

        var jahresbeleg = new RksvReminderJahresbelegDto
        {
            IsRequired = jbRequired,
            DaysUntilDeadline = jbDays,
            Status = jbStatus,
            FonRequired = fonRequired,
            FonStatus = fonReminderStatus,
            FonDaysUntilDeadline = fonDays,
            FonSubmissionStatus = fonSubmissionStatus,
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

    private async Task<(bool Required, string Status, int? Days, string? SubmissionStatus)> ResolveJahresbelegFonAsync(
        Guid cashRegisterId,
        int jahresbelegYear,
        bool hasJahresbeleg,
        DateTime viennaToday,
        CancellationToken cancellationToken)
    {
        if (!hasJahresbeleg)
            return (false, MbOk, null, null);

        var paymentId = await _db.PaymentDetails.AsNoTracking()
            .Where(p =>
                p.CashRegisterId == cashRegisterId
                && p.IsActive
                && (
                    (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg
                     && p.RksvSpecialReceiptYear == jahresbelegYear)
                    || (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg
                        && p.RksvSpecialReceiptYear == jahresbelegYear
                        && p.RksvSpecialReceiptMonth == 12)))
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        string? submission = null;
        if (paymentId is Guid pid)
        {
            submission = await _db.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking()
                .Where(s => s.PaymentId == pid)
                .Select(s => s.Status)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (string.Equals(submission, RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Verified, StringComparison.OrdinalIgnoreCase))
            return (false, MbOk, null, submission);

        var deadline = AutoMonatsbelegCutoff.JahresbelegFonDeadline(jahresbelegYear);
        var days = (deadline - viennaToday.Date).Days;
        var overdue = viennaToday.Date > deadline;
        return (true, overdue ? MbOverdue : MbUpcoming, Math.Max(0, days), submission ?? RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending);
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
