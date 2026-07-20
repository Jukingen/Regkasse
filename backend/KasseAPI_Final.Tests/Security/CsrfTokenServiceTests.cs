using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests.Security;

public sealed class CsrfTokenServiceTests
{
    [Fact]
    public void GenerateToken_ReturnsValidToken()
    {
        var service = CreateService();

        var token = service.GenerateToken();

        Assert.NotEmpty(token);
        Assert.True(service.ValidateToken(token, token));
    }

    [Fact]
    public void ValidateToken_ExpiredToken_ReturnsFalse()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(cache);

        var token = service.GenerateToken();
        Assert.True(service.ValidateToken(token, token));

        // Simulate expiry by evicting the cache entry (same outcome as AbsoluteExpiration).
        cache.Remove($"csrf_{token}");

        Assert.False(service.ValidateToken(token, token));
    }

    [Fact]
    public void ValidateToken_Mismatch_ReturnsFalse()
    {
        var service = CreateService();
        var token = service.GenerateToken();

        Assert.False(service.ValidateToken(token, "wrong"));
        Assert.False(service.ValidateToken(token, token + "x"));
    }

    [Fact]
    public void ValidateToken_MissingValues_ReturnsFalse()
    {
        var service = CreateService();
        var token = service.GenerateToken();

        Assert.False(service.ValidateToken("", token));
        Assert.False(service.ValidateToken(token, ""));
        Assert.False(service.ValidateToken(null!, null!));
    }

    [Fact]
    public void ValidateToken_UnknownTokenNotInCache_ReturnsFalse()
    {
        var service = CreateService();
        const string forged = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        Assert.False(service.ValidateToken(forged, forged));
    }

    private static CsrfTokenService CreateService(IMemoryCache? cache = null, int lifetimeHours = 24)
    {
        var monitor = new OptionsMonitorStub(new CsrfOptions
        {
            Enabled = true,
            TokenLifetimeHours = lifetimeHours,
        });
        return new CsrfTokenService(
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            NullLogger<CsrfTokenService>.Instance,
            monitor);
    }

    private sealed class OptionsMonitorStub : IOptionsMonitor<CsrfOptions>
    {
        public OptionsMonitorStub(CsrfOptions current) => CurrentValue = current;

        public CsrfOptions CurrentValue { get; }

        public CsrfOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<CsrfOptions, string?> listener) =>
            new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
