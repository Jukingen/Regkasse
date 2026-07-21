using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Aşağı seviye düzeltme (Tagesbericht / Monatsbericht) sonrası üst artefaktları (Monatsbericht / Jahresbericht)
/// otomatik yeniden hesaplamaz; sadece «inceleme gerekli» bayraklarını işaretler. Finalize snapshot’ları sessizce değiştirmez.
/// </summary>
public static class FormalReportPropagationMarkers
{
    public const string ReasonTagesSupersededInMonth = "tages_superseded_in_month";
    public const string ReasonMonatsSupersededInYear = "monats_superseded_in_year";

    /// <summary>Tagesbericht-Korrektur: betroffene Monats- und Jahresberichte (gleicher Kalendermonat bzw. Jahr) markieren.</summary>
    public static async Task MarkAfterTagesCorrectionAsync(
        AppDbContext db,
        DateTime viennaBusinessDate,
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var monthStart = new DateTime(viennaBusinessDate.Year, viennaBusinessDate.Month, 1);
        await MarkMonatsActiveRowsAsync(db, monthStart, MonatsberichtScopeKinds.Register, cashRegisterId, ReasonTagesSupersededInMonth, cancellationToken);
        await MarkMonatsActiveRowsAsync(db, monthStart, MonatsberichtScopeKinds.Company, null, ReasonTagesSupersededInMonth, cancellationToken);
        await MarkJahresActiveRowsAsync(db, monthStart.Year, MonatsberichtScopeKinds.Register, cashRegisterId, ReasonTagesSupersededInMonth, cancellationToken);
        await MarkJahresActiveRowsAsync(db, monthStart.Year, MonatsberichtScopeKinds.Company, null, ReasonTagesSupersededInMonth, cancellationToken);
    }

    /// <summary>Monatsbericht-Korrektur: betroffene Jahresberichte markieren (kein stiller Monats-Mix).</summary>
    public static async Task MarkAfterMonatsCorrectionAsync(
        AppDbContext db,
        DateTime viennaMonthStart,
        string scopeKind,
        Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var sk = (scopeKind ?? MonatsberichtScopeKinds.Register).Trim();
        if (sk == MonatsberichtScopeKinds.Register && !cashRegisterId.HasValue)
            return;
        if (sk == MonatsberichtScopeKinds.Company && cashRegisterId.HasValue)
            return;

        await MarkJahresActiveRowsAsync(db, viennaMonthStart.Year, sk, cashRegisterId, ReasonMonatsSupersededInYear, cancellationToken);
    }

    public static void ClearUpstreamReview(MonatsberichtReport row)
    {
        row.UpstreamReviewRequired = false;
        row.UpstreamReviewReasonCode = null;
    }

    public static void ClearUpstreamReview(JahresberichtReport row)
    {
        row.UpstreamReviewRequired = false;
        row.UpstreamReviewReasonCode = null;
    }

    private static async Task MarkMonatsActiveRowsAsync(
        AppDbContext db,
        DateTime monthStart,
        string scopeKind,
        Guid? cashRegisterId,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        var q = db.Set<MonatsberichtReport>().Where(x =>
            x.ViennaMonthStart == monthStart &&
            x.ScopeKind == scopeKind &&
            x.SupersededByReportId == null &&
            (scopeKind == MonatsberichtScopeKinds.Company ? x.CashRegisterId == null : x.CashRegisterId == cashRegisterId));

        var rows = await q.ToListAsync(cancellationToken);
        foreach (var r in rows)
        {
            if (r.ReportStatus == MonatsberichtReportStatuses.Superseded)
                continue;
            r.UpstreamReviewRequired = true;
            r.UpstreamReviewReasonCode = reasonCode;
        }
    }

    private static async Task MarkJahresActiveRowsAsync(
        AppDbContext db,
        int calendarYear,
        string scopeKind,
        Guid? cashRegisterId,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        var yearStart = new DateTime(calendarYear, 1, 1);
        var q = db.Set<JahresberichtReport>().Where(x =>
            x.ViennaYearStart == yearStart &&
            x.ScopeKind == scopeKind &&
            x.SupersededByReportId == null &&
            (scopeKind == MonatsberichtScopeKinds.Company ? x.CashRegisterId == null : x.CashRegisterId == cashRegisterId));

        var rows = await q.ToListAsync(cancellationToken);
        foreach (var r in rows)
        {
            if (r.ReportStatus == MonatsberichtReportStatuses.Superseded)
                continue;
            r.UpstreamReviewRequired = true;
            r.UpstreamReviewReasonCode = reasonCode;
        }
    }
}
