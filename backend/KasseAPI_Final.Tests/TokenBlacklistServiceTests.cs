using KasseAPI_Final.Services.Token;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TokenBlacklistServiceTests
{
    private static TokenBlacklistService CreateService(IMemoryCache? cache = null) =>
        new(cache ?? new MemoryCache(new MemoryCacheOptions()), NullLogger<TokenBlacklistService>.Instance);

    [Fact]
    public void BlacklistToken_RejectsSameTokenUntilExpiry()
    {
        var service = CreateService();
        const string token = "eyJhbGciOiJIUzI1NiJ9.payload.signature";

        Assert.False(service.IsTokenBlacklisted(token));

        service.BlacklistToken(token, DateTime.UtcNow.AddHours(1));

        Assert.True(service.IsTokenBlacklisted(token));
        Assert.True(service.IsTokenBlacklisted("Bearer " + token));
    }

    [Fact]
    public void BlacklistToken_DoesNotAffectOtherTokens()
    {
        var service = CreateService();
        service.BlacklistToken("token-a", DateTime.UtcNow.AddMinutes(30));

        Assert.True(service.IsTokenBlacklisted("token-a"));
        Assert.False(service.IsTokenBlacklisted("token-b"));
    }

    [Fact]
    public void BlacklistToken_IgnoresAlreadyExpiredTokens()
    {
        var service = CreateService();
        const string token = "expired-token";

        service.BlacklistToken(token, DateTime.UtcNow.AddSeconds(-5));

        Assert.False(service.IsTokenBlacklisted(token));
    }

    [Fact]
    public void BlacklistToken_IgnoresEmptyInput()
    {
        var service = CreateService();
        service.BlacklistToken(" ", DateTime.UtcNow.AddHours(1));
        Assert.False(service.IsTokenBlacklisted(" "));
        Assert.False(service.IsTokenBlacklisted(string.Empty));
    }
}
