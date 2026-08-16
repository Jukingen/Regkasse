using KasseAPI_Final.Configuration;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyOptionsTests
{
    [Fact]
    public void Enabled_DefaultsToTrue()
    {
        Assert.True(new FiskalyOptions().Enabled);
        Assert.True(new FiskalyOptions().IsEffectivelyEnabled(null));
    }

    [Fact]
    public void ScuId_AliasesSignatureCreationUnitId()
    {
        var opts = new FiskalyOptions { ScuId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa" };
        Assert.Equal("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", opts.SignatureCreationUnitId);
        Assert.Equal(opts.SignatureCreationUnitId, opts.TseSerialNumber);
    }

    [Fact]
    public void TokenCacheHours_WritesMinutes()
    {
        var opts = new FiskalyOptions { TokenCacheHours = 24 };
        Assert.Equal(1440, opts.TokenCacheMinutes);
        Assert.Equal(24, opts.TokenCacheHours);
    }

    [Fact]
    public void ResolveEnvironment_UsesExplicitLive()
    {
        var opts = new FiskalyOptions
        {
            Environment = "LIVE",
            ApiBaseUrl = "https://rksv.fiskaly.com/api/v1"
        };
        Assert.Equal("LIVE", opts.ResolveEnvironment());
    }

    [Fact]
    public void HasApiCredentials_IgnoresEnabledFlag()
    {
        var opts = new FiskalyOptions
        {
            Enabled = false,
            ApiKey = "k",
            ApiSecret = "s"
        };
        Assert.True(opts.HasApiCredentials);
        Assert.False(opts.HasCredentials);
        Assert.True(opts.HasActiveCredentials(true));
        Assert.False(opts.HasActiveCredentials(false));
    }
}
