using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyTseServiceTests
{
    [Fact]
    public async Task AuthenticateAsync_WithoutCredentials_Throws()
    {
        var svc = CreateService(new FiskalyOptions { Enabled = false });
        var ex = await Assert.ThrowsAsync<FiskalyApiException>(() => svc.AuthenticateAsync());
        Assert.Contains("ApiKey", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateTssAsync_InvalidId_Throws()
    {
        var svc = CreateService(EnabledOptions());
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateTssAsync("not-a-uuid"));
    }

    [Fact]
    public async Task SignTransactionAsync_RequiresCashRegisterId()
    {
        var svc = CreateService(EnabledOptions());
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SignTransactionAsync(
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            new FiskalyTransactionData { TotalAmount = 1m }));
    }

    [Fact]
    public async Task EnsureResourcesForCashRegisterAsync_CreatesScuAndClient()
    {
        var scuId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var registerId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var client = new Mock<IFiskalyClient>();
        client.Setup(c => c.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyAuthResult(true, DateTimeOffset.UtcNow.AddHours(24), 8));
        client.Setup(c => c.CreateSignatureCreationUnitAsync(
                scuId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyScuInfo(scuId.ToString("D"), "CREATED", null));
        client.Setup(c => c.CreateCashRegisterAsync(registerId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyCashRegisterInfo(registerId.ToString("D"), "CREATED"));

        var options = EnabledOptions();
        options.SignatureCreationUnitId = scuId.ToString("D");
        var svc = CreateService(options, client.Object);

        var result = await svc.EnsureResourcesForCashRegisterAsync(
            Guid.NewGuid(), registerId, "KASSE-1");

        Assert.True(result.Success);
        Assert.Equal(scuId.ToString("D"), result.ScuId);
        Assert.Equal(registerId.ToString("D"), result.CashRegisterId);
        Assert.Equal("CREATED", result.ScuState);
    }

    [Fact]
    public async Task IsReadyToSignAsync_RequiresInitializedScuAndRegister()
    {
        var scuId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var registerId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var client = new Mock<IFiskalyClient>();
        client.Setup(c => c.GetSignatureCreationUnitAsync(scuId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyScuInfo(scuId.ToString("D"), FiskalyResourceStates.Initialized, "SN"));
        client.Setup(c => c.GetCashRegisterAsync(registerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyCashRegisterInfo(registerId.ToString("D"), FiskalyResourceStates.Initialized));

        var options = EnabledOptions();
        options.SignatureCreationUnitId = scuId.ToString("D");
        var svc = CreateService(options, client.Object);

        Assert.True(await svc.IsReadyToSignAsync(registerId));
        Assert.False(await svc.IsReadyToSignAsync(Guid.Empty));
    }

    [Fact]
    public async Task IsReadyToSignAsync_ScuCreated_ReturnsFalse()
    {
        var scuId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var registerId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var client = new Mock<IFiskalyClient>();
        client.Setup(c => c.GetSignatureCreationUnitAsync(scuId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyScuInfo(scuId.ToString("D"), FiskalyResourceStates.Created, null));

        var options = EnabledOptions();
        options.SignatureCreationUnitId = scuId.ToString("D");
        var svc = CreateService(options, client.Object);

        Assert.False(await svc.IsReadyToSignAsync(registerId));
        client.Verify(c => c.GetCashRegisterAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelReceiptAsync_ForcesCancellationReceiptType()
    {
        var registerId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var client = new Mock<IFiskalyClient>();
        client.Setup(c => c.SignReceiptAsync(
                registerId,
                receiptId,
                It.Is<FiskalyTransactionData>(d => d.ReceiptType == "CANCELLATION" && d.TotalAmount == -10m),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalySignedReceipt(
                receiptId.ToString("D"),
                registerId.ToString("D"),
                "SIGNED",
                "_R1-AT1_KASSE-1_2_2026-08-16T12:00:00_-10,00_0,00_0,00_0,00_0,00_abc_123_0_sig",
                "2",
                "TEST",
                Signed: true,
                ReceiptType: "CANCELLATION"));

        var svc = CreateService(EnabledOptions(), client.Object);
        var signed = await svc.CancelReceiptAsync(
            Guid.NewGuid().ToString("D"),
            receiptId.ToString("D"),
            new FiskalyTransactionData
            {
                CashRegisterId = registerId.ToString("D"),
                ReceiptType = "NORMAL",
                TotalAmount = -10m
            });

        Assert.True(signed.Signed);
        Assert.Equal("CANCELLATION", signed.ReceiptType);
        client.Verify(
            c => c.SignReceiptAsync(
                registerId,
                receiptId,
                It.Is<FiskalyTransactionData>(d => d.ReceiptType == "CANCELLATION"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static FiskalyOptions EnabledOptions() => new()
    {
        Enabled = true,
        ApiKey = "test-key",
        ApiSecret = "test-secret",
        ApiBaseUrl = "https://rksv.fiskaly.com/api/v1"
    };

    private static FiskalyTseService CreateService(FiskalyOptions options, IFiskalyClient? client = null)
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"fiskaly_tse_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(dbOptions, NullCurrentTenantAccessor.Instance);
        return new FiskalyTseService(
            client ?? Mock.Of<IFiskalyClient>(),
            Options.Create(options).ToMonitor(),
            db,
            NullLogger<FiskalyTseService>.Instance);
    }
}
