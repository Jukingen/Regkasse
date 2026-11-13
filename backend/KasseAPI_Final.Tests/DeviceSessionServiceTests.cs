using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Session;
using KasseAPI_Final.Services.Token;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DeviceSessionServiceTests
{
    [Fact]
    public async Task GetActiveSessionsAsync_maps_device_fields()
    {
        var inner = new Mock<IUserSessionService>();
        inner.Setup(s => s.GetActiveSessionsAsync("u1", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ActiveSessionDto
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    DeviceName = "Chrome · Windows",
                    Browser = "Chrome",
                    OS = "Windows",
                    IpAddress = "127.0.0.1",
                    StartedAtUtc = DateTime.UtcNow.AddHours(-2),
                    LastActivityAtUtc = DateTime.UtcNow.AddMinutes(-5),
                    IsActive = true,
                    IsCurrent = true,
                    ClientApp = "admin",
                },
            });

        var sut = new DeviceSessionService(inner.Object, CreateBlacklist());
        var list = await sut.GetActiveSessionsAsync("u1", null);

        var row = Assert.Single(list);
        Assert.Equal("Chrome", row.Browser);
        Assert.Equal("Windows", row.OS);
        Assert.Equal("127.0.0.1", row.IPAddress);
        Assert.True(row.IsCurrent);
    }

    [Fact]
    public async Task RevokeSessionAsync_blacklists_current_access_token_only()
    {
        var sessionId = Guid.NewGuid();
        var inner = new Mock<IUserSessionService>();
        inner.Setup(s => s.TerminateSessionAsync("u1", sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var blacklist = CreateBlacklist();
        var sut = new DeviceSessionService(inner.Object, blacklist);

        const string token = "access-token-value";
        var ok = await sut.RevokeSessionAsync(
            "u1",
            sessionId,
            currentSessionId: sessionId,
            currentAccessToken: token,
            currentAccessTokenExpiresAtUtc: DateTime.UtcNow.AddHours(1));

        Assert.True(ok);
        Assert.True(blacklist.IsTokenBlacklisted(token));
    }

    [Fact]
    public async Task RevokeSessionAsync_does_not_blacklist_when_revoking_other_session()
    {
        var otherId = Guid.NewGuid();
        var currentId = Guid.NewGuid();
        var inner = new Mock<IUserSessionService>();
        inner.Setup(s => s.TerminateSessionAsync("u1", otherId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var blacklist = CreateBlacklist();
        var sut = new DeviceSessionService(inner.Object, blacklist);

        const string token = "current-access-token";
        await sut.RevokeSessionAsync(
            "u1",
            otherId,
            currentSessionId: currentId,
            currentAccessToken: token,
            currentAccessTokenExpiresAtUtc: DateTime.UtcNow.AddHours(1));

        Assert.False(blacklist.IsTokenBlacklisted(token));
    }

    private static TokenBlacklistService CreateBlacklist() =>
        new(new MemoryCache(new MemoryCacheOptions()), NullLogger<TokenBlacklistService>.Instance);
}
