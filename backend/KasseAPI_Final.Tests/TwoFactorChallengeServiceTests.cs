using KasseAPI_Final.Services.Auth;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TwoFactorChallengeServiceTests
{
    [Fact]
    public void CreateChallenge_ThenConsume_ReturnsPayloadOnce()
    {
        var service = new TwoFactorChallengeService(new MemoryCache(new MemoryCacheOptions()));
        var payload = new TwoFactorChallengePayload(
            "user-1",
            "admin",
            "admin@test.com",
            SetupRequired: true,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(5));

        var token = service.CreateChallenge(payload);

        Assert.True(service.TryConsumeChallenge(token, out var consumed));
        Assert.NotNull(consumed);
        Assert.Equal("user-1", consumed!.UserId);
        Assert.True(consumed.SetupRequired);

        Assert.False(service.TryConsumeChallenge(token, out _));
    }

    [Fact]
    public void TryConsumeChallenge_Expired_ReturnsFalse()
    {
        var service = new TwoFactorChallengeService(new MemoryCache(new MemoryCacheOptions()));
        var payload = new TwoFactorChallengePayload(
            "user-1",
            "admin",
            "admin@test.com",
            SetupRequired: false,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        var token = service.CreateChallenge(payload);

        Assert.False(service.TryConsumeChallenge(token, out _));
    }

    [Fact]
    public void TryConsumeChallenge_UnknownToken_ReturnsFalse()
    {
        var service = new TwoFactorChallengeService(new MemoryCache(new MemoryCacheOptions()));
        Assert.False(service.TryConsumeChallenge("missing", out _));
    }
}
