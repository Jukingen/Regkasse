using KasseAPI_Final.Services;
using Moq;

namespace KasseAPI_Final.Tests;

/// <summary>Test doubles for <see cref="IRksvMonatsbelegPolicy"/> (default: gate off).</summary>
internal static class RksvMonatsbelegTestDoubles
{
    public static IRksvMonatsbelegPolicy GateOff()
    {
        var m = new Mock<IRksvMonatsbelegPolicy>();
        m.SetupGet(p => p.SessionGateApplies).Returns(false);
        return m.Object;
    }

    public static IRksvMonatsbelegPolicy GateOnMissingMonatsbeleg()
    {
        var m = new Mock<IRksvMonatsbelegPolicy>();
        m.SetupGet(p => p.SessionGateApplies).Returns(true);
        m.Setup(p => p.HasMonatsbelegForRegisterMonthAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return m.Object;
    }

    public static IRksvMonatsbelegPolicy GateOnHasMonatsbeleg()
    {
        var m = new Mock<IRksvMonatsbelegPolicy>();
        m.SetupGet(p => p.SessionGateApplies).Returns(true);
        m.Setup(p => p.HasMonatsbelegForRegisterMonthAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return m.Object;
    }
}
