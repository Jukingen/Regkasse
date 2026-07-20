using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Cache;
using KasseAPI_Final.Services.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Integration tests against a live Redis on localhost:6379.
/// Skipped when Redis is unavailable (CI without Redis).
/// </summary>
public sealed class RedisCacheServiceTests : IDisposable
{
    private readonly IConnectionMultiplexer? _mux;
    private readonly bool _redisAvailable;

    public RedisCacheServiceTests()
    {
        try
        {
            _mux = ConnectionMultiplexer.Connect(
                "127.0.0.1:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000");
            _ = _mux.GetDatabase().Ping();
            _redisAvailable = true;
        }
        catch
        {
            _redisAvailable = false;
            _mux?.Dispose();
            _mux = null;
        }
    }

    public void Dispose() => _mux?.Dispose();

    [SkippableFact]
    public async Task GetOrCreateAsync_CachesFactoryResult()
    {
        Skip.If(!_redisAvailable, "Redis is not running on localhost:6379");
        var sut = CreateSut();
        var key = $"test:getorcreate:{Guid.NewGuid():N}";
        var calls = 0;

        var first = await sut.GetOrCreateAsync(key, async () =>
        {
            calls++;
            await Task.Yield();
            return 42;
        });
        var second = await sut.GetOrCreateAsync(key, () =>
        {
            calls++;
            return Task.FromResult(99);
        });

        Assert.Equal(42, first);
        Assert.Equal(42, second);
        Assert.Equal(1, calls);

        await sut.RemoveAsync(key);
    }

    [SkippableFact]
    public async Task RemoveAsync_ForcesFactoryToRunAgain()
    {
        Skip.If(!_redisAvailable, "Redis is not running on localhost:6379");
        var sut = CreateSut();
        var key = $"test:remove:{Guid.NewGuid():N}";
        var calls = 0;
        Func<Task<string>> factory = () =>
        {
            calls++;
            return Task.FromResult($"v{calls}");
        };

        Assert.Equal("v1", await sut.GetOrCreateAsync(key, factory));
        await sut.RemoveAsync(key);
        Assert.Equal("v2", await sut.GetOrCreateAsync(key, factory));
        Assert.Equal(2, calls);

        await sut.RemoveAsync(key);
    }

    [SkippableFact]
    public async Task RemoveByPrefixAsync_RemovesMatchingKeysOnly()
    {
        Skip.If(!_redisAvailable, "Redis is not running on localhost:6379");
        var sut = CreateSut();
        var suffix = Guid.NewGuid().ToString("N");
        var p1 = $"products:{suffix}:t1";
        var p2 = $"products:{suffix}:t2";
        var other = $"other:{suffix}:x";

        await sut.GetOrCreateAsync(p1, () => Task.FromResult("a"));
        await sut.GetOrCreateAsync(p2, () => Task.FromResult("b"));
        await sut.GetOrCreateAsync(other, () => Task.FromResult("c"));

        await sut.RemoveByPrefixAsync($"products:{suffix}:");

        var productsCalls = 0;
        var otherCalls = 0;
        var products = await sut.GetOrCreateAsync(p1, () =>
        {
            productsCalls++;
            return Task.FromResult("a2");
        });
        var otherValue = await sut.GetOrCreateAsync(other, () =>
        {
            otherCalls++;
            return Task.FromResult("c2");
        });

        Assert.Equal("a2", products);
        Assert.Equal(1, productsCalls);
        Assert.Equal("c", otherValue);
        Assert.Equal(0, otherCalls);

        await sut.RemoveAsync(other);
    }

    [SkippableFact]
    public async Task ExistsAsync_ReflectsKeyPresence()
    {
        Skip.If(!_redisAvailable, "Redis is not running on localhost:6379");
        var sut = CreateSut();
        var key = $"test:exists:{Guid.NewGuid():N}";

        Assert.False(await sut.ExistsAsync(key));
        await sut.GetOrCreateAsync(key, () => Task.FromResult(1));
        Assert.True(await sut.ExistsAsync(key));
        await sut.RemoveAsync(key);
        Assert.False(await sut.ExistsAsync(key));
    }

    [SkippableFact]
    public async Task InstanceName_PrefixesKeysInRedis()
    {
        Skip.If(!_redisAvailable, "Redis is not running on localhost:6379");
        var sut = CreateSut();
        var logicalKey = $"test:prefix:{Guid.NewGuid():N}";
        await sut.GetOrCreateAsync(logicalKey, () => Task.FromResult("x"));

        var raw = await _mux!.GetDatabase().StringGetAsync($"Regkasse_Test:{logicalKey}");
        Assert.True(raw.HasValue);

        await sut.RemoveAsync(logicalKey);
    }

    private RedisCacheService CreateSut()
    {
        var options = Options.Create(new RedisOptions
        {
            ConnectionString = "localhost:6379",
            InstanceName = "Regkasse_Test",
        });
        return new RedisCacheService(
            _mux!,
            options,
            NullLogger<RedisCacheService>.Instance,
            new CacheMetricsService());
    }
}
