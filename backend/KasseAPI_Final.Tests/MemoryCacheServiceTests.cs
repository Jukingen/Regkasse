using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Metrics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class MemoryCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_CachesFactoryResult()
    {
        var sut = CreateSut();
        var calls = 0;

        var first = await sut.GetOrCreateAsync("k1", async _ =>
        {
            calls++;
            await Task.Yield();
            return 42;
        });
        var second = await sut.GetOrCreateAsync("k1", _ =>
        {
            calls++;
            return Task.FromResult(99);
        });

        Assert.Equal(42, first);
        Assert.Equal(42, second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RemoveAsync_ForcesFactoryToRunAgain()
    {
        var sut = CreateSut();
        var calls = 0;
        Func<CancellationToken, Task<string>> factory = _ =>
        {
            calls++;
            return Task.FromResult($"v{calls}");
        };

        Assert.Equal("v1", await sut.GetOrCreateAsync("k2", factory));
        await sut.RemoveAsync("k2");
        Assert.Equal("v2", await sut.GetOrCreateAsync("k2", factory));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_RemovesMatchingKeysOnly()
    {
        var sut = CreateSut();
        await sut.GetOrCreateAsync("products:t1", _ => Task.FromResult("a"));
        await sut.GetOrCreateAsync("products:t2", _ => Task.FromResult("b"));
        await sut.GetOrCreateAsync("other:x", _ => Task.FromResult("c"));

        await sut.RemoveByPrefixAsync("products:");

        var productsCalls = 0;
        var otherCalls = 0;
        var products = await sut.GetOrCreateAsync("products:t1", _ =>
        {
            productsCalls++;
            return Task.FromResult("a2");
        });
        var other = await sut.GetOrCreateAsync("other:x", _ =>
        {
            otherCalls++;
            return Task.FromResult("c2");
        });

        Assert.Equal("a2", products);
        Assert.Equal(1, productsCalls);
        Assert.Equal("c", other);
        Assert.Equal(0, otherCalls);
    }

    [Fact]
    public async Task ExistsAsync_ReflectsKeyPresence()
    {
        var sut = CreateSut();
        Assert.False(await sut.ExistsAsync("missing"));

        await sut.GetOrCreateAsync("present", _ => Task.FromResult(1));
        Assert.True(await sut.ExistsAsync("present"));

        await sut.RemoveAsync("present");
        Assert.False(await sut.ExistsAsync("present"));
    }

    [Fact]
    public async Task GetSetAsync_RoundTripsValue()
    {
        var sut = CreateSut();
        await sut.SetAsync("g1", "hello", TimeSpan.FromMinutes(1));
        Assert.Equal("hello", await sut.GetAsync<string>("g1"));
    }

    [Fact]
    public async Task ClearAllAsync_RemovesTrackedKeys()
    {
        var sut = CreateSut();
        await sut.SetAsync("a", 1);
        await sut.SetAsync("b", 2);
        await sut.ClearAllAsync();
        Assert.False(await sut.ExistsAsync("a"));
        Assert.False(await sut.ExistsAsync("b"));
    }

    private static MemoryCacheService CreateSut() =>
        new(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<MemoryCacheService>.Instance,
            new CacheMetricsService());
}
