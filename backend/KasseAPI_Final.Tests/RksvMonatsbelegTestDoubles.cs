using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Time;
using Moq;

namespace KasseAPI_Final.Tests;

/// <summary>Test doubles for <see cref="IRksvMonatsbelegPolicy"/> (default: gate off).</summary>
internal static class RksvMonatsbelegTestDoubles
{
    public static IRksvMonatsbelegPolicy GateOff()
    {
        var m = new Mock<IRksvMonatsbelegPolicy>();
        m.SetupGet(p => p.SessionGateApplies).Returns(false);
        m.Setup(p => p.HasMonatsbelegForRegisterMonthAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        m.Setup(p => p.EvaluateSalesGateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AllowDecision(sessionGateApplies: false, previousMonthMissing: false));
        return m.Object;
    }

    public static IRksvMonatsbelegPolicy GateOnMissingMonatsbeleg()
    {
        var m = new Mock<IRksvMonatsbelegPolicy>();
        m.SetupGet(p => p.SessionGateApplies).Returns(true);
        m.Setup(p => p.HasMonatsbelegForRegisterMonthAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        m.Setup(p => p.EvaluateSalesGateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BlockDecision());
        return m.Object;
    }

    public static IRksvMonatsbelegPolicy GateOnHasMonatsbeleg()
    {
        var m = new Mock<IRksvMonatsbelegPolicy>();
        m.SetupGet(p => p.SessionGateApplies).Returns(true);
        m.Setup(p => p.HasMonatsbelegForRegisterMonthAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        m.Setup(p => p.EvaluateSalesGateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AllowDecision(sessionGateApplies: true, previousMonthMissing: false));
        return m.Object;
    }

    /// <summary>Gate on: previous completed Vienna month exists, current unfinished month does not.</summary>
    public static IRksvMonatsbelegPolicy GateOnHasPreviousMonthOnly()
    {
        var (prevYear, prevMonth) = PostgreSqlUtcDateTime.GetViennaPreviousYearMonth();
        var m = new Mock<IRksvMonatsbelegPolicy>();
        m.SetupGet(p => p.SessionGateApplies).Returns(true);
        m.Setup(p => p.HasMonatsbelegForRegisterMonthAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, int y, int month, CancellationToken _) => y == prevYear && month == prevMonth);
        m.Setup(p => p.EvaluateSalesGateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AllowDecision(sessionGateApplies: true, previousMonthMissing: false));
        return m.Object;
    }

    public static IRksvMonatsbelegPolicy GateOnMissingAllowSales()
    {
        var m = new Mock<IRksvMonatsbelegPolicy>();
        m.SetupGet(p => p.SessionGateApplies).Returns(true);
        m.Setup(p => p.HasMonatsbelegForRegisterMonthAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        m.Setup(p => p.EvaluateSalesGateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AllowDecision(sessionGateApplies: true, previousMonthMissing: true));
        return m.Object;
    }

    private static MonatsbelegSalesGateDecision BlockDecision() =>
        new(
            SessionGateApplies: true,
            PreviousMonthMissing: true,
            Mode: Models.MonatsbelegBlockingMode.Strict,
            ViennaDayOfMonth: 1,
            BlocksSales: true,
            WarningLevel: MonatsbelegSalesGateEvaluator.WarningRed,
            CanContinueWithWarning: false,
            WarningMessageDe: "Monatsbeleg fehlt.");

    private static MonatsbelegSalesGateDecision AllowDecision(bool sessionGateApplies, bool previousMonthMissing) =>
        new(
            sessionGateApplies,
            previousMonthMissing,
            previousMonthMissing ? Models.MonatsbelegBlockingMode.WarningOnly : Models.MonatsbelegBlockingMode.Strict,
            ViennaDayOfMonth: 1,
            BlocksSales: false,
            WarningLevel: previousMonthMissing ? MonatsbelegSalesGateEvaluator.WarningRed : MonatsbelegSalesGateEvaluator.WarningNone,
            CanContinueWithWarning: previousMonthMissing,
            WarningMessageDe: previousMonthMissing ? "Monatsbeleg fehlt." : null);
}
