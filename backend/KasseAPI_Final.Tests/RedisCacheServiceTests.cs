using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Metrics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// RedisCacheService tests against <see cref="MemoryDistributedCache"/> (no live Redis required).
/// </summary>
public sealed class RedisCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_CachesFactoryResult()
    {
        var sut = CreateSut();
        var key = $"test:getorcreate:{Guid.NewGuid():N}";
        var calls = 0;

        var first = await sut.GetOrCreateAsync(key, async _ =>
        {
            calls++;
            await Task.Yield();
            return 42;
        });
        var second = await sut.GetOrCreateAsync(key, _ =>
        {
            calls++;
            return Task.FromResult(99);
        });

        Assert.Equal(42, first);
        Assert.Equal(42, second);
        Assert.Equal(1, calls);

        await sut.RemoveAsync(key);
    }

    [Fact]
    public async Task RemoveAsync_ForcesFactoryToRunAgain()
    {
        var sut = CreateSut();
        var key = $"test:remove:{Guid.NewGuid():N}";
        var calls = 0;
        Func<CancellationToken, Task<string>> factory = _ =>
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

    [Fact]
    public async Task RemoveByPrefixAsync_RemovesMatchingKeysOnly()
    {
        var sut = CreateSut();
        var prefix = $"products:{Guid.NewGuid():N}:";
        var other = $"other:{Guid.NewGuid():N}";

        await sut.GetOrCreateAsync(prefix + "t1", _ => Task.FromResult("a"));
        await sut.GetOrCreateAsync(prefix + "t2", _ => Task.FromResult("b"));
        await sut.GetOrCreateAsync(other, _ => Task.FromResult("c"));

        await sut.RemoveByPrefixAsync(prefix);

        var productsCalls = 0;
        var otherCalls = 0;
        var products = await sut.GetOrCreateAsync(prefix + "t1", _ =>
        {
            productsCalls++;
            return Task.FromResult("a2");
        });
        var otherValue = await sut.GetOrCreateAsync(other, _ =>
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

    [Fact]
    public async Task GetSetAsync_RoundTripsJsonPayload()
    {
        var sut = CreateSut();
        var key = $"test:getset:{Guid.NewGuid():N}";
        await sut.SetAsync(key, new CacheProbeDto { Name = "regkasse" });
        var loaded = await sut.GetAsync<CacheProbeDto>(key);
        Assert.NotNull(loaded);
        Assert.Equal("regkasse", loaded!.Name);
        await sut.RemoveAsync(key);
    }

    [Fact]
    public async Task WhenRedisFails_FallsBackToMemory_AndIsRedisAvailableBecomesFalse()
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        var distributed = new ThrowingDistributedCache();
        var sut = new RedisCacheService(
            distributed,
            memory,
            NullLogger<RedisCacheService>.Instance,
            new CacheMetricsService());

        Assert.True(sut.IsRedisAvailable);

        await sut.SetAsync("k1", "via-memory");
        Assert.False(sut.IsRedisAvailable);
        Assert.Equal("via-memory", await sut.GetAsync<string>("k1"));

        // Recovery path: swap in a working distributed cache via successful ops is not possible
        // with a permanently throwing backend; flag stays false until a Redis success.
        Assert.False(sut.IsRedisAvailable);
    }

    [Fact]
    public async Task WhenRedisSucceeds_IsRedisAvailableRemainsTrue()
    {
        var sut = CreateSut();
        Assert.True(sut.IsRedisAvailable);
        await sut.SetAsync("ok", 1);
        Assert.True(sut.IsRedisAvailable);
        Assert.Equal(1, await sut.GetAsync<int>("ok"));
        Assert.True(sut.IsRedisAvailable);
        await sut.RemoveAsync("ok");
    }

    [Fact]
    public async Task WhenRedisRecovers_IsRedisAvailableBecomesTrueAgain()
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        var distributed = new ToggleableDistributedCache();
        var sut = new RedisCacheService(
            distributed,
            memory,
            NullLogger<RedisCacheService>.Instance,
            new CacheMetricsService());

        distributed.Throw = true;
        await sut.SetAsync("recover-key", "fallback");
        Assert.False(sut.IsRedisAvailable);

        distributed.Throw = false;
        await sut.SetAsync("recover-key", "redis-again");
        Assert.True(sut.IsRedisAvailable);
        Assert.Equal("redis-again", await sut.GetAsync<string>("recover-key"));
    }

    private sealed class CacheProbeDto
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Simulates Redis outage (name contains Redis so IsTransientCacheFailure matches).</summary>
    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new IOException("Redis connection refused");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromException<byte[]?>(new IOException("Redis connection refused"));

        public void Refresh(string key) => throw new IOException("Redis connection refused");

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            Task.FromException(new IOException("Redis connection refused"));

        public void Remove(string key) => throw new IOException("Redis connection refused");

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            Task.FromException(new IOException("Redis connection refused"));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw new IOException("Redis connection refused");

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default) =>
            Task.FromException(new IOException("Redis connection refused"));
    }

    /// <summary>Distributed cache that can simulate Redis outage then recovery.</summary>
    private sealed class ToggleableDistributedCache : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner =
            new(Options.Create(new MemoryDistributedCacheOptions()));

        public bool Throw { get; set; }

        public byte[]? Get(string key) => Throw
            ? throw new IOException("Redis connection refused")
            : _inner.Get(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Throw
                ? Task.FromException<byte[]?>(new IOException("Redis connection refused"))
                : _inner.GetAsync(key, token);

        public void Refresh(string key)
        {
            if (Throw) throw new IOException("Redis connection refused");
            _inner.Refresh(key);
        }

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            Throw
                ? Task.FromException(new IOException("Redis connection refused"))
                : _inner.RefreshAsync(key, token);

        public void Remove(string key)
        {
            if (Throw) throw new IOException("Redis connection refused");
            _inner.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            Throw
                ? Task.FromException(new IOException("Redis connection refused"))
                : _inner.RemoveAsync(key, token);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            if (Throw) throw new IOException("Redis connection refused");
            _inner.Set(key, value, options);
        }

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default) =>
            Throw
                ? Task.FromException(new IOException("Redis connection refused"))
                : _inner.SetAsync(key, value, options, token);
    }

    private static RedisCacheService CreateSut()
    {
        IDistributedCache distributed = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new RedisCacheService(
            distributed,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<RedisCacheService>.Instance,
            new CacheMetricsService());
    }
}
