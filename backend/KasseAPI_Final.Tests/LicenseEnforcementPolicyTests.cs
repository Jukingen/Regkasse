using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

[Collection("OpenApiExportWebHost")]
public sealed class LicenseEnforcementPolicyTests
{
    public LicenseEnforcementPolicyTests()
    {
        OpenApiExportHostGate.EnsureExportModeDisabled();
    }
    [Fact]
    public void ShouldDisableEnforcement_WhenLicenseDisabled_ReturnsTrue()
    {
        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);
        var license = new LicenseOptions { Enabled = false };

        Assert.True(LicenseEnforcementPolicy.ShouldDisableEnforcement(env, licenseOptions: license));
    }

    [Fact]
    public void ShouldDisableEnforcement_InDevelopment_ReturnsTrue()
    {
        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development);

        Assert.True(LicenseEnforcementPolicy.ShouldDisableEnforcement(env));
    }

    [Fact]
    public void ShouldDisableEnforcement_InProductionWithDemoTse_ReturnsTrue()
    {
        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);

        var tse = new TseOptions { TseMode = "Demo" };

        Assert.True(LicenseEnforcementPolicy.ShouldDisableEnforcement(env, tse));
    }

    [Fact]
    public void ShouldDisableEnforcement_InProductionDeviceMode_ReturnsFalse()
    {
        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);

        var devMode = new Mock<IDevelopmentModeService>();
        devMode.Setup(x => x.ShouldBypassLicense()).Returns(false);

        Assert.False(LicenseEnforcementPolicy.ShouldDisableEnforcement(
            env,
            new TseOptions { TseMode = "Device" },
            devMode.Object));
    }

    [Fact]
#pragma warning disable CS0618
    public void GetMaxOfflineTransactions_InDevelopment_ReturnsUnlimited()
    {
        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development);

        Assert.Equal(
            LicenseEnforcementPolicy.MaxOfflineTransactionsUnlimited,
            LicenseEnforcementPolicy.GetMaxOfflineTransactionsPerCashRegister(env));
    }

    [Fact]
    public void GetMaxOfflineTransactions_InProduction_ReturnsConfiguredCap()
    {
        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);

        var tse = new TseOptions { TseMode = "Device", MaxOfflineTransactionsPerCashRegister = 50 };

        Assert.Equal(50, LicenseEnforcementPolicy.GetMaxOfflineTransactionsPerCashRegister(env, tse));
    }

    [Fact]
    public void GetMaxOfflineTransactions_InProductionDemoTse_ReturnsUnlimited()
    {
        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Production);

        var tse = new TseOptions { TseMode = "Demo", MaxOfflineTransactionsPerCashRegister = 50 };

        Assert.Equal(
            LicenseEnforcementPolicy.MaxOfflineTransactionsUnlimited,
            LicenseEnforcementPolicy.GetMaxOfflineTransactionsPerCashRegister(env, tse));
    }
#pragma warning restore CS0618
}
