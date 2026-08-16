using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseServiceSoftFallbackTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_soft_status_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TseService CreateService(
        AppDbContext db,
        IFiskalyTseService fiskalyTse,
        TseOptions tseOptions,
        string environmentName,
        ISoftTseService? softTse = null,
        bool providerReady = true)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environmentName);
        var key = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(key, NullLogger<SignaturePipeline>.Instance);
        var provider = new Mock<ITseProvider>();
        provider.Setup(p => p.IsReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(providerReady);
        var fiskalyOpts = Options.Create(new FiskalyOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            ApiSecret = "test-secret",
            ApiBaseUrl = "https://rksv.fiskaly.com/api/v1",
        }).ToMonitor();

        return new TseService(
            db,
            pipeline,
            key,
            provider.Object,
            Mock.Of<ILogger<TseService>>(),
            env.Object,
            developmentOptions: null,
            developmentModeService: null,
            fiskalyTse,
            fiskalyOpts,
            Options.Create(tseOptions).ToMonitor(),
            softTse);
    }

    private static TseDevice SeedDisconnectedDevice(AppDbContext db)
    {
        var device = new TseDevice
        {
            SerialNumber = "scu-dev",
            DeviceType = "fiskaly",
            VendorId = "VID",
            ProductId = "PID",
            IsConnected = false,
            CanCreateInvoices = false,
            CertificateStatus = "UNKNOWN",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.TseDevices.Add(device);
        db.SaveChanges();
        return device;
    }

    [Fact]
    public async Task GetTseStatusAsync_FiskalyFails_DevelopmentFallback_ReturnsSoftFallback()
    {
        await using var db = CreateDb();
        SeedDisconnectedDevice(db);

        var fiskaly = new Mock<IFiskalyTseService>();
        fiskaly.Setup(s => s.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("fiskaly down"));

        var svc = CreateService(
            db,
            fiskaly.Object,
            new TseOptions { FallbackEnabled = true, SoftTseEnabled = true },
            Environments.Development,
            new SoftTseService(
                new FakeTseProvider(NullLogger<FakeTseProvider>.Instance),
                NullLogger<SoftTseService>.Instance));

        var status = await svc.GetTseStatusAsync();

        Assert.Equal("SoftFallback", status.Status);
        Assert.True(status.IsConnected);
        Assert.True(status.IsReady);
        Assert.True(status.IsOperational);
        Assert.Contains("fiskaly down", status.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTseStatusAsync_FiskalyFails_Production_DoesNotFallback()
    {
        await using var db = CreateDb();
        SeedDisconnectedDevice(db);

        var fiskaly = new Mock<IFiskalyTseService>();
        fiskaly.Setup(s => s.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("fiskaly down"));

        var svc = CreateService(
            db,
            fiskaly.Object,
            new TseOptions { FallbackEnabled = true, SoftTseEnabled = true },
            Environments.Production,
            new SoftTseService(
                new FakeTseProvider(NullLogger<FakeTseProvider>.Instance),
                NullLogger<SoftTseService>.Instance));

        var status = await svc.GetTseStatusAsync();

        Assert.NotEqual("SoftFallback", status.Status);
        Assert.False(status.IsConnected);
        Assert.Contains("fiskaly down", status.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateDailyClosingSignatureAsync_Production_WhenProviderNotReady_Throws()
    {
        await using var db = CreateDb();
        SeedDisconnectedDevice(db);

        var fiskaly = new Mock<IFiskalyTseService>();
        var svc = CreateService(
            db,
            fiskaly.Object,
            new TseOptions { FallbackEnabled = true, SoftTseEnabled = true },
            Environments.Production,
            providerReady: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateDailyClosingSignatureAsync(
                Guid.NewGuid(),
                "KASSE-1",
                DateTime.UtcNow.Date,
                10m,
                1));

        Assert.Contains("not allowed in Production", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
