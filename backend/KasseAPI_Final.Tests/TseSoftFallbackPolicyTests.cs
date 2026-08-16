using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseSoftFallbackPolicyTests
{
    private static IHostEnvironment Env(string name)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(name);
        return env.Object;
    }

    [Fact]
    public void IsAllowed_Development_WithBothFlags_IsTrue()
    {
        var opts = new TseOptions { FallbackEnabled = true, SoftTseEnabled = true };
        Assert.True(TseSoftFallbackPolicy.IsAllowed(opts, Env(Environments.Development)));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void IsAllowed_Development_MissingFlag_IsFalse(bool fallback, bool soft)
    {
        var opts = new TseOptions { FallbackEnabled = fallback, SoftTseEnabled = soft };
        Assert.False(TseSoftFallbackPolicy.IsAllowed(opts, Env(Environments.Development)));
    }

    [Fact]
    public void IsAllowed_Production_EvenWithFlags_IsFalse()
    {
        var opts = new TseOptions { FallbackEnabled = true, SoftTseEnabled = true };
        Assert.False(TseSoftFallbackPolicy.IsAllowed(opts, Env(Environments.Production)));
    }

    [Fact]
    public void IsAllowed_NullEnvironment_IsFalse()
    {
        var opts = new TseOptions { FallbackEnabled = true, SoftTseEnabled = true };
        Assert.False(TseSoftFallbackPolicy.IsAllowed(opts, null));
    }
}
