using System;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineAdminConnectivityServiceTests
{
    private static IOptionsMonitor<T> MonitorOf<T>(T value) where T : class, new()
    {
        var mock = new Mock<IOptionsMonitor<T>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static TseDevice CreateDevice() =>
        new()
        {
            FinanzOnlineUsername = "u",
            FinanzOnlineEnabled = true,
            LastFinanzOnlineSync = DateTime.UtcNow,
            PendingInvoices = 1,
            PendingReports = 0
        };

    [Fact]
    public async Task BuildStatusAsync_WhenAnyTransportSimulated_IsConnectedFalse_AndDiagnosticsSet()
    {
        var sessionMock = new Mock<IFinanzOnlineSessionClient>(MockBehavior.Strict);
        var service = new FinanzOnlineAdminConnectivityService(
            sessionMock.Object,
            MonitorOf(new FinanzOnlineSessionOptions { UseSimulation = true }),
            MonitorOf(new FinanzOnlineRegistrierkassenOptions { UseSimulation = false }),
            MonitorOf(new FinanzOnlineTransmissionQueryOptions { UseSimulation = false }),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<FinanzOnlineAdminConnectivityService>.Instance);

        var snap = await service.BuildStatusAsync(CreateDevice(), CancellationToken.None);

        Assert.False(snap.IsConnected);
        Assert.False(snap.IsAuthoritative);
        Assert.True(snap.FinanzOnlineTransportsSimulated);
        Assert.Contains("Session.UseSimulation=True", snap.TransportDiagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTestConnectionAsync_WhenSimulated_DoesNotCallSessionClient_AndSuccessFalse()
    {
        var sessionMock = new Mock<IFinanzOnlineSessionClient>(MockBehavior.Strict);
        var service = new FinanzOnlineAdminConnectivityService(
            sessionMock.Object,
            MonitorOf(new FinanzOnlineSessionOptions { UseSimulation = true }),
            MonitorOf(new FinanzOnlineRegistrierkassenOptions()),
            MonitorOf(new FinanzOnlineTransmissionQueryOptions()),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<FinanzOnlineAdminConnectivityService>.Instance);

        var test = await service.RunTestConnectionAsync(CreateDevice(), CancellationToken.None);

        Assert.False(test.Success);
        Assert.False(test.IsAuthoritative);
        Assert.True(test.FinanzOnlineTransportsSimulated);
        sessionMock.Verify(
            x => x.GetValidSessionAsync(It.IsAny<FinanzOnlineSessionLoginRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunTestConnectionAsync_WhenReal_UsesSessionProbe_AndCachesForStatus()
    {
        var sessionMock = new Mock<IFinanzOnlineSessionClient>();
        sessionMock
            .Setup(x => x.GetValidSessionAsync(It.IsAny<FinanzOnlineSessionLoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanzOnlineSessionAccessResult
            {
                Success = true,
                SessionToken = "tok",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
            });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new FinanzOnlineAdminConnectivityService(
            sessionMock.Object,
            MonitorOf(new FinanzOnlineSessionOptions { UseSimulation = false }),
            MonitorOf(new FinanzOnlineRegistrierkassenOptions { UseSimulation = false }),
            MonitorOf(new FinanzOnlineTransmissionQueryOptions { UseSimulation = false }),
            cache,
            NullLogger<FinanzOnlineAdminConnectivityService>.Instance);

        var test = await service.RunTestConnectionAsync(CreateDevice(), CancellationToken.None);
        Assert.True(test.Success);
        Assert.True(test.IsAuthoritative);

        var status = await service.BuildStatusAsync(CreateDevice(), CancellationToken.None);
        Assert.True(status.IsConnected);
        Assert.True(status.IsAuthoritative);
        Assert.True(status.SessionProbeSucceeded);
    }
}
