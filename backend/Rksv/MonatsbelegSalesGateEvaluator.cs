using KasseAPI_Final.Models;

namespace KasseAPI_Final.Rksv;

/// <summary>POS sales-gate decision when the previous Vienna-month Monatsbeleg is missing.</summary>
public sealed record MonatsbelegSalesGateDecision(
    bool SessionGateApplies,
    bool PreviousMonthMissing,
    MonatsbelegBlockingMode Mode,
    int ViennaDayOfMonth,
    bool BlocksSales,
    string WarningLevel,
    bool CanContinueWithWarning,
    string? WarningMessageDe);

/// <summary>
/// Calendar policy for Monatsbeleg sales blocking (Vienna day-of-month).
/// Product policy — RKSV does not require blocking sales when Monatsbeleg is late.
/// </summary>
public static class MonatsbelegSalesGateEvaluator
{
    public const string WarningNone = "none";
    public const string WarningYellow = "yellow";
    public const string WarningRed = "red";

    /// <summary>RKSV create-within-7-days window (inclusive).</summary>
    public const int RksvDeadlineDay = MonatsbelegPastMonthPolicy.MonatsbelegGraceDays;

    /// <summary>GracePeriod mode allows sales through this Vienna calendar day (inclusive).</summary>
    public const int GraceAllowThroughDay = 14;

    public static MonatsbelegSalesGateDecision Evaluate(
        bool sessionGateApplies,
        bool previousMonthMissing,
        MonatsbelegBlockingMode mode,
        int viennaDayOfMonth)
    {
        var day = viennaDayOfMonth < 1 ? 1 : viennaDayOfMonth > 31 ? 31 : viennaDayOfMonth;

        if (!sessionGateApplies || !previousMonthMissing)
        {
            return new MonatsbelegSalesGateDecision(
                sessionGateApplies,
                previousMonthMissing,
                mode,
                day,
                BlocksSales: false,
                WarningLevel: WarningNone,
                CanContinueWithWarning: false,
                WarningMessageDe: null);
        }

        var warningLevel = day <= RksvDeadlineDay || day > GraceAllowThroughDay
            ? WarningRed
            : WarningYellow;

        var blocksSales = mode switch
        {
            MonatsbelegBlockingMode.WarningOnly => false,
            MonatsbelegBlockingMode.GracePeriod => day > GraceAllowThroughDay,
            _ => true,
        };

        var canContinue = !blocksSales;

        var warningMessageDe = warningLevel == WarningYellow
            ? "Monatsbeleg fehlt. Verkäufe sind mit Warnung erlaubt. Bitte Mandanten-Admin kontaktieren."
            : "Monatsbeleg fehlt. Bitte Mandanten-Admin kontaktieren.";

        return new MonatsbelegSalesGateDecision(
            sessionGateApplies,
            previousMonthMissing,
            mode,
            day,
            blocksSales,
            warningLevel,
            canContinue,
            warningMessageDe);
    }
}
