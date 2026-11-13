using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalySetupServiceTests
{
    [Fact]
    public async Task AuthenticateFon_Disabled_Returns400()
    {
        var client = new Mock<IFiskalyClient>(MockBehavior.Strict);
        var svc = CreateService(new FiskalyOptions { Enabled = false }, client.Object);

        var result = await svc.AuthenticateFonAsync(
            new AuthenticateFonRequest { FonParticipantId = "12345678", FonUserId = "user1", FonUserPin = "pin12" },
            "sa-1");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
        client.Verify(c => c.AuthenticateFonAsync(It.IsAny<FiskalyFonAuthRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitializeScu_RequiresFonAuthentication()
    {
        var client = new Mock<IFiskalyClient>();
        client.Setup(c => c.GetFonAuthStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyFonAuthResult(false, null, null, "NOT_AUTHENTICATED", null));
        var svc = CreateService(EnabledOptions(), client.Object);

        var result = await svc.InitializeScuAsync("sa-1");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("FON", result.Message, StringComparison.OrdinalIgnoreCase);
        client.Verify(
            c => c.UpdateSignatureCreationUnitStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InitializeScu_AlreadyInitialized_DoesNotPatchAgain()
    {
        var scuId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var client = new Mock<IFiskalyClient>();
        client.Setup(c => c.GetFonAuthStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyFonAuthResult(true, "12345678", "user1", "AUTHENTICATED", DateTimeOffset.UtcNow));
        client.Setup(c => c.GetSignatureCreationUnitAsync(scuId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyScuInfo(scuId.ToString("D"), "INITIALIZED", null));

        var opts = EnabledOptions();
        opts.ScuId = scuId.ToString("D");
        var svc = CreateService(opts, client.Object);

        var result = await svc.InitializeScuAsync("sa-1");

        Assert.True(result.Success);
        Assert.Equal("INITIALIZED", result.Data!.State);
        client.Verify(
            c => c.UpdateSignatureCreationUnitStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InitializeCashRegister_MissingRegister_Returns404()
    {
        var client = new Mock<IFiskalyClient>();
        client.Setup(c => c.GetFonAuthStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyFonAuthResult(true, "12345678", "user1", "AUTHENTICATED", DateTimeOffset.UtcNow));
        var scuId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var cashRegisters = new Mock<ICashRegisterManagementService>();
        cashRegisters.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashRegisterDto?)null);
        var opts = EnabledOptions();
        opts.ScuId = scuId.ToString("D");
        client.Setup(c => c.GetSignatureCreationUnitAsync(scuId.ToString("D"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyScuInfo(scuId.ToString("D"), "INITIALIZED", null));

        var svc = CreateService(opts, client.Object, cashRegisters.Object);
        var result = await svc.InitializeCashRegisterAsync(Guid.NewGuid(), "sa-1", actorIsSuperAdmin: true);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        client.Verify(
            c => c.UpdateCashRegisterStateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static FiskalyOptions EnabledOptions() => new()
    {
        Enabled = true,
        ApiKey = "k",
        ApiSecret = "s",
        ScuId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
    };

    private static FiskalySetupService CreateService(
        FiskalyOptions options,
        IFiskalyClient client,
        ICashRegisterManagementService? cashRegisters = null)
    {
        var (factory, cache) = CreateStore(options);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        return new FiskalySetupService(
            Options.Create(options).ToMonitor(),
            cache,
            client,
            factory,
            cashRegisters ?? Mock.Of<ICashRegisterManagementService>(),
            audit.Object,
            NullLogger<FiskalySetupService>.Instance);
    }

    private static (IDbContextFactory<AppDbContext> Factory, FiskalyEnabledOverrideCache Cache) CreateStore(
        FiskalyOptions _,
        ICurrentTenantAccessor? tenantAccessor = null)
    {
        var dbName = $"FiskalySetup_{Guid.NewGuid():N}";
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() => CreateDb(dbName));
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => CreateDb(dbName));
        var cache = new FiskalyEnabledOverrideCache(
            factory.Object,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<FiskalyEnabledOverrideCache>.Instance,
            tenantAccessor);
        return (factory.Object, cache);
    }

    private static AppDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
    }
}
