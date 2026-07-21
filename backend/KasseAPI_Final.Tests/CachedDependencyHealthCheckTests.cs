using KasseAPI_Final.Configuration;
using KasseAPI_Final.HealthChecks;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseCachedHealthCheckTests
{
    [Fact]
    public async Task Online_ReturnsHealthy()
    {
        var monitor = new Mock<ITseHealthMonitor>();
        monitor.SetupGet(m => m.Snapshot).Returns(new TseHealthSnapshot
        {
            Status = TseOperationalHealth.Online,
            LastCheckUtc = DateTime.UtcNow,
            LastSuccessfulPingUtc = DateTime.UtcNow,
        });

        var result = await new TseCachedHealthCheck(monitor.Object)
            .CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Theory]
    [InlineData(TseOperationalHealth.Degraded)]
    [InlineData(TseOperationalHealth.Offline)]
    public async Task NonOnline_ReturnsDegraded(TseOperationalHealth status)
    {
        var monitor = new Mock<ITseHealthMonitor>();
        monitor.SetupGet(m => m.Snapshot).Returns(new TseHealthSnapshot
        {
            Status = status,
            LastCheckUtc = DateTime.UtcNow,
            ConsecutiveFailures = 3,
        });

        var result = await new TseCachedHealthCheck(monitor.Object)
            .CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }
}

public sealed class NtpCachedHealthCheckTests
{
    private static IOptionsMonitor<NtpSettings> MonitorOf(NtpSettings value)
    {
        var mock = new Mock<IOptionsMonitor<NtpSettings>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    [Fact]
    public async Task WhenFiscalAllowed_ReturnsHealthy()
    {
        var ntp = new Mock<INtpTimeSyncStatus>();
        ntp.Setup(n => n.BuildStatusDto(It.IsAny<NtpSettings>()))
            .Returns(new DTOs.SystemTimeStatusDto
            {
                IsSynchronized = true,
                WarningLevel = "ok",
                OffsetSeconds = 0.1,
                LastSyncAt = DateTime.UtcNow,
            });
        string? msg;
        ntp.Setup(n => n.ShouldAllowOnlineFiscalPayment(It.IsAny<NtpSettings>(), out msg))
            .Returns(true);

        var check = new NtpCachedHealthCheck(ntp.Object, MonitorOf(new NtpSettings { Enabled = true }));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task WhenFiscalBlocked_ReturnsDegraded()
    {
        var ntp = new Mock<INtpTimeSyncStatus>();
        ntp.Setup(n => n.BuildStatusDto(It.IsAny<NtpSettings>()))
            .Returns(new DTOs.SystemTimeStatusDto
            {
                IsSynchronized = false,
                WarningLevel = "critical",
                OffsetSeconds = 999,
            });
        var blockedMsg = "NTP nicht synchron";
        ntp.Setup(n => n.ShouldAllowOnlineFiscalPayment(It.IsAny<NtpSettings>(), out blockedMsg))
            .Returns(false);

        var check = new NtpCachedHealthCheck(ntp.Object, MonitorOf(new NtpSettings { Enabled = true }));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("NTP", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenNtpDisabled_ReturnsHealthy()
    {
        var ntp = new Mock<INtpTimeSyncStatus>();
        ntp.Setup(n => n.BuildStatusDto(It.IsAny<NtpSettings>()))
            .Returns(new DTOs.SystemTimeStatusDto { IsSynchronized = true, WarningLevel = "ok" });

        var check = new NtpCachedHealthCheck(ntp.Object, MonitorOf(new NtpSettings { Enabled = false }));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("disabled", result.Description, StringComparison.OrdinalIgnoreCase);
    }
}
