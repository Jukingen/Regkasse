using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using KasseAPI_Final.Tse.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseProviderFactoryTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_factory_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TseProviderFactory CreateFactory(
        TseOptions tse,
        FiskalyOptions? fiskaly = null,
        AppDbContext? db = null)
    {
        fiskaly ??= new FiskalyOptions { Enabled = false };
        db ??= CreateDb();
        var key = Mock.Of<ITseKeyProvider>();
        return new TseProviderFactory(
            new FakeTseProvider(NullLogger<FakeTseProvider>.Instance),
            new RealTseProvider(
                new SignaturePipeline(key, NullLogger<SignaturePipeline>.Instance),
                key,
                db,
                NullLogger<RealTseProvider>.Instance),
            Options.Create(tse).ToMonitor(),
            Options.Create(fiskaly).ToMonitor(),
            NullLogger<TseProviderFactory>.Instance);
    }

    [Fact]
    public void GetProvider_Fake_ReturnsFakeTseProvider()
    {
        var factory = CreateFactory(new TseOptions { Mode = "Fake", TseMode = "Demo" });
        Assert.IsType<FakeTseProvider>(factory.GetProvider("fake"));
        Assert.IsType<FakeTseProvider>(factory.GetProvider("soft"));
    }

    [Fact]
    public void GetProvider_Fiskaly_ReturnsRealTseProvider()
    {
        var factory = CreateFactory(new TseOptions { Mode = "Real", TseMode = "Device", Provider = "fiskaly" });
        Assert.IsType<RealTseProvider>(factory.GetProvider("fiskaly"));
        Assert.IsType<RealTseProvider>(factory.GetProvider("FISKALY"));
    }

    [Theory]
    [InlineData("epson")]
    [InlineData("swissbit")]
    public async Task GetProvider_UnsupportedVendors_ReturnStub(string vendor)
    {
        var factory = CreateFactory(new TseOptions { Mode = "Real", TseMode = "Device", Provider = vendor });
        var provider = factory.GetProvider(vendor);
        var stub = Assert.IsType<UnsupportedVendorTseProvider>(provider);
        Assert.Equal(vendor, stub.VendorName);
        Assert.False(await provider.IsReadyAsync());
    }

    [Fact]
    public void GetProvider_Unknown_Throws()
    {
        var factory = CreateFactory(new TseOptions());
        Assert.Throws<ArgumentException>(() => factory.GetProvider("unknown-vendor"));
        Assert.Throws<ArgumentException>(() => factory.GetProvider(""));
    }

    [Fact]
    public void ResolveConfiguredProviderName_FakeMode_ReturnsFake()
    {
        var factory = CreateFactory(new TseOptions { Mode = "Fake", TseMode = "Device", Provider = "fiskaly" });
        Assert.Equal(TseOptions.ProviderFake, factory.ResolveConfiguredProviderName());
        Assert.IsType<FakeTseProvider>(factory.GetConfiguredProvider());
    }

    [Fact]
    public void ResolveConfiguredProviderName_DemoWithoutProvider_ReturnsSoft()
    {
        var factory = CreateFactory(new TseOptions { Mode = "Real", TseMode = "Demo", Provider = null });
        Assert.Equal(TseOptions.ProviderSoft, factory.ResolveConfiguredProviderName());
    }

    [Fact]
    public void IsProviderConfigured_Fiskaly_UsesProvidersBlock()
    {
        var tse = new TseOptions
        {
            Mode = "Real",
            TseMode = "Device",
            Provider = "fiskaly",
            Providers =
            {
                ["fiskaly"] = new TseVendorConnectionOptions
                {
                    Enabled = true,
                    ApiKey = "key",
                    ApiSecret = "secret",
                    ApiBaseUrl = "https://example.test",
                },
            },
        };

        var factory = CreateFactory(tse, new FiskalyOptions { Enabled = false });
        Assert.True(factory.IsProviderConfigured("fiskaly"));
        Assert.False(factory.IsProviderConfigured("epson"));
    }

    [Fact]
    public void IsProviderConfigured_Epson_RequiresCredentials()
    {
        var tse = new TseOptions
        {
            Providers =
            {
                ["epson"] = new TseVendorConnectionOptions
                {
                    Enabled = true,
                    ApiKey = "e-key",
                    ApiSecret = "e-secret",
                },
            },
        };
        var factory = CreateFactory(tse);
        Assert.True(factory.IsProviderConfigured("epson"));
        Assert.False(factory.IsProviderConfigured("swissbit"));
    }

    [Fact]
    public void GetKnownProviderNames_IncludesCoreVendors()
    {
        var names = CreateFactory(new TseOptions()).GetKnownProviderNames();
        Assert.Contains(TseOptions.ProviderFiskaly, names);
        Assert.Contains(TseOptions.ProviderEpson, names);
        Assert.Contains(TseOptions.ProviderSwissbit, names);
        Assert.Contains(TseOptions.ProviderFake, names);
    }
}
