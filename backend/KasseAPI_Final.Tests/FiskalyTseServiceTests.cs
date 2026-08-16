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
