using KasseAPI_Final.Services.Monitoring;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SentrySamplingTests
{
    [Fact]
    public void Defaults_AreTenPercentTraces_AndFiftyPercentErrors()
    {
        Assert.Equal(0.1, SentrySampling.Resolve(null, null, SentrySampling.DefaultTracesSampleRate));
        Assert.Equal(0.5, SentrySampling.Resolve(null, null, SentrySampling.DefaultErrorSampleRate));
    }

    [Fact]
    public void EnvironmentValue_WinsOverConfiguration()
    {
        Assert.Equal(0.25, SentrySampling.Resolve("0.25", "0.9", 0.1));
        Assert.Equal(1.0, SentrySampling.Resolve("1.0", null, 0.5));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("nope")]
    [InlineData("-0.1")]
    [InlineData("1.5")]
    public void InvalidValues_FallBack(string raw)
    {
        Assert.Equal(0.1, SentrySampling.Resolve(raw, "2", 0.1));
        Assert.Equal(0.4, SentrySampling.Resolve(raw, "0.4", 0.1));
    }
}
