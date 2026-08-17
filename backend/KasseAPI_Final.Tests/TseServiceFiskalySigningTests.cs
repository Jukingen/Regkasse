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

public sealed class TseServiceFiskalySigningTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_fiskaly_sign_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TseService CreateService(
        AppDbContext db,
        IFiskalyTseService? fiskalyTse,
        FiskalyOptions fiskalyOptions,
        string environmentName,
        ISoftTseService? softTse = null,
        TseOptions? tseOptions = null)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environmentName);
        var key = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(key, NullLogger<SignaturePipeline>.Instance);
        var provider = new Mock<ITseProvider>();
        provider.Setup(p => p.IsReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

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
            Options.Create(fiskalyOptions).ToMonitor(),
            Options.Create(tseOptions ?? new TseOptions()).ToMonitor(),
            softTse);
    }

    [Fact]
    public async Task CreateInvoiceSignature_FiskalyEnabledButNotReady_ThrowsUnavailable()
    {
        await using var db = CreateDb();
        var fiskaly = new Mock<IFiskalyTseService>();
        fiskaly.Setup(s => s.IsReadyToSignAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var svc = CreateService(
            db,
            fiskaly.Object,
            new FiskalyOptions { Enabled = true, ApiKey = "k", ApiSecret = "s" },
            Environments.Development);

        var ex = await Assert.ThrowsAsync<TseUnavailableException>(() =>
            svc.CreateInvoiceSignatureAsync(
                Guid.NewGuid(),
                "AT-1",
                10m,
                "KASSE-1"));

        Assert.Contains("TSE is not available", ex.Message, StringComparison.OrdinalIgnoreCase);
        fiskaly.Verify(
            s => s.SignTransactionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<FiskalyTransactionData>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateInvoiceSignature_ProductionWithoutFiskaly_ThrowsUnavailable()
    {
        await using var db = CreateDb();
        var svc = CreateService(
            db,
            fiskalyTse: null,
            new FiskalyOptions { Enabled = false },
            Environments.Production,
            softTse: new SoftTseService(
                new FakeTseProvider(NullLogger<FakeTseProvider>.Instance),
                NullLogger<SoftTseService>.Instance),
            tseOptions: new TseOptions { FallbackEnabled = true, SoftTseEnabled = true });

        var ex = await Assert.ThrowsAsync<TseUnavailableException>(() =>
            svc.CreateInvoiceSignatureAsync(
                Guid.NewGuid(),
                "AT-1",
                10m,
                "KASSE-1"));

        Assert.Contains("TSE is not available", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateInvoiceSignature_FiskalyReady_SignsReceipt()
    {
        await using var db = CreateDb();
        var registerId = Guid.NewGuid();
        var fiskaly = new Mock<IFiskalyTseService>();
        fiskaly.Setup(s => s.IsReadyToSignAsync(registerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        fiskaly.Setup(s => s.SignTransactionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<FiskalyTransactionData>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalySignedReceipt(
                Guid.NewGuid().ToString("D"),
                registerId.ToString("D"),
                "SIGNED",
                "_R1-AT1_KASSE-1_1_2026-08-16T12:00:00_10,00_0,00_0,00_0,00_0,00_abc_123_0_sig",
                "1",
                "TEST",
                TimeSignature: 1,
                Signed: true,
                CashRegisterSerialNumber: "KASSE-1"));

        var svc = CreateService(
            db,
            fiskaly.Object,
            new FiskalyOptions { Enabled = true, ApiKey = "k", ApiSecret = "s" },
            Environments.Production);

        // In-memory provider cannot run PostgreSQL chain SQL after Fiskaly returns.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            svc.CreateInvoiceSignatureAsync(registerId, "AT-1", 10m, "KASSE-1"));

        fiskaly.Verify(
            s => s.SignTransactionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<FiskalyTransactionData>(d =>
                    d.ReceiptType == "NORMAL"
                    && d.AmountsPerVatRate != null
                    && d.AmountsPerVatRate.Count > 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
