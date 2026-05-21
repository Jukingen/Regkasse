using Xunit;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.AdminCashRegisters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace KasseAPI_Final.Tests;

public sealed class CashRegisterDecommissionServiceTests
{
    [Fact]
    public void IsHardDeleteAllowed_RequiresDevelopmentAndConfig()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        var options = Options.Create(new CashRegisterComplianceOptions { AllowHardDelete = true });
        var svc = new CashRegisterDecommissionService(
            null!,
            null!,
            null!,
            null!,
            env.Object,
            options,
            NullLogger<CashRegisterDecommissionService>.Instance);

        Assert.True(svc.IsHardDeleteAllowed());

        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        Assert.False(svc.IsHardDeleteAllowed());

        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var off = Options.Create(new CashRegisterComplianceOptions { AllowHardDelete = false });
        var svc2 = new CashRegisterDecommissionService(
            null!,
            null!,
            null!,
            null!,
            env.Object,
            off,
            NullLogger<CashRegisterDecommissionService>.Instance);
        Assert.False(svc2.IsHardDeleteAllowed());
    }
}
