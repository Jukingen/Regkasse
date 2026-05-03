using KasseAPI_Final.Services;
using Moq;

namespace KasseAPI_Final.Tests;

/// <summary>Test doubles for <see cref="IRksvStartbelegPolicy"/> (default: gate off).</summary>
internal static class RksvStartbelegTestDoubles
{
    public static IRksvStartbelegPolicy GateOff()
    {
        var m = new Mock<IRksvStartbelegPolicy>();
        m.SetupGet(p => p.SessionGateApplies).Returns(false);
        return m.Object;
    }

    public static IRksvStartbelegPolicy GateOnMissingStartbeleg()
    {
        var m = new Mock<IRksvStartbelegPolicy>();
        m.SetupGet(p => p.SessionGateApplies).Returns(true);
        m.Setup(p => p.HasStartbelegForRegisterAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return m.Object;
    }

    public static IRksvStartbelegPolicy GateOnHasStartbeleg()
    {
        var m = new Mock<IRksvStartbelegPolicy>();
        m.SetupGet(p => p.SessionGateApplies).Returns(true);
        m.Setup(p => p.HasStartbelegForRegisterAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return m.Object;
    }
}
