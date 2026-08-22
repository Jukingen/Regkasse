using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalySignTestServiceTests
{
    [Fact]
    public async Task Sign_Disabled_Returns400()
    {
        var client = new Mock<IFiskalyClient>(MockBehavior.Strict);
        var svc = CreateService(new FiskalyOptions { Enabled = false }, client.Object);

        var result = await svc.SignAsync(
            new FiskalySignTestRequest { CashRegisterId = Guid.NewGuid() },
            "sa-1",
            actorIsSuperAdmin: true);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
        client.Verify(
            c => c.SignReceiptAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<FiskalyTransactionData>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Sign_LiveEnvironment_Returns400()
    {
        var client = new Mock<IFiskalyClient>(MockBehavior.Strict);
        var svc = CreateService(
            new FiskalyOptions
            {
                Enabled = true,
                ApiKey = "k",
                ApiSecret = "s",
                Environment = FiskalyOptions.LiveEnvironment
            },
            client.Object);

        var result = await svc.SignAsync(
            new FiskalySignTestRequest { CashRegisterId = Guid.NewGuid() },
            "sa-1",
            actorIsSuperAdmin: true);

        Assert.False(result.Success);
        Assert.Contains("LIVE", result.Message, StringComparison.OrdinalIgnoreCase);
        client.Verify(
            c => c.SignReceiptAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<FiskalyTransactionData>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Sign_MonthlyClose_Returns400WithoutHttp()
    {
        var registerId = Guid.NewGuid();
        var client = new Mock<IFiskalyClient>();
        client
            .Setup(c => c.GetCashRegisterAsync(registerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyCashRegisterInfo(registerId.ToString("D"), FiskalyResourceStates.Initialized));

        var cashRegisters = new Mock<ICashRegisterManagementService>();
        cashRegisters
            .Setup(c => c.GetByIdAsync(registerId, It.IsAny<Guid?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CashRegisterDto { Id = registerId, RegisterNumber = "K1" });

        var svc = CreateService(EnabledTestOptions(), client.Object, cashRegisters.Object);

        var result = await svc.SignAsync(
            new FiskalySignTestRequest { CashRegisterId = registerId, Scenario = FiskalySignTestScenarioIds.MonthlyClose },
            "sa-1",
            actorIsSuperAdmin: true);

        Assert.False(result.Success);
        Assert.Contains("automatically", result.Message, StringComparison.OrdinalIgnoreCase);
        client.Verify(
            c => c.SignReceiptAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<FiskalyTransactionData>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Sign_Normal_ReturnsSignedReceiptAndValidatesQr()
    {
        var registerId = Guid.NewGuid();
        const string qr =
            "_R1-AT3_dGxx_19_2017-10-24T11:07:32_10,00_0,00_0,00_0,00_0,00_7eti9M9dETz2_5474185F_M8LJDeWizNY=_sig";

        var client = new Mock<IFiskalyClient>();
        client
            .Setup(c => c.GetCashRegisterAsync(registerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyCashRegisterInfo(registerId.ToString("D"), FiskalyResourceStates.Initialized));
        client
            .Setup(c => c.SignReceiptAsync(
                registerId,
                It.IsAny<Guid>(),
                It.Is<FiskalyTransactionData>(d => d.ReceiptType == "NORMAL"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid id, FiskalyTransactionData _, CancellationToken _) =>
                new FiskalySignedReceipt(
                    id.ToString("D"),
                    registerId.ToString("D"),
                    "SIGNED",
                    qr,
                    "19",
                    "TEST",
                    1577833200,
                    Signed: true,
                    CashRegisterSerialNumber: "1",
                    ReceiptType: "NORMAL"));

        var cashRegisters = new Mock<ICashRegisterManagementService>();
        cashRegisters
            .Setup(c => c.GetByIdAsync(registerId, It.IsAny<Guid?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CashRegisterDto { Id = registerId, RegisterNumber = "K1" });

        var svc = CreateService(EnabledTestOptions(), client.Object, cashRegisters.Object);

        var result = await svc.SignAsync(
            new FiskalySignTestRequest { CashRegisterId = registerId, Scenario = FiskalySignTestScenarioIds.Normal },
            "sa-1",
            actorIsSuperAdmin: true);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("19", result.Data!.ReceiptNumber);
        Assert.True(result.Data.QrValidation.IsValid);
        Assert.True(result.Data.Checks.Signed);
        Assert.True(result.Data.Checks.ReceiptNumberLooksSequential);
    }

    [Fact]
    public async Task Verify_MissingRegister_Returns404()
    {
        var registerId = Guid.NewGuid();
        var client = new Mock<IFiskalyClient>(MockBehavior.Strict);
        var cashRegisters = new Mock<ICashRegisterManagementService>();
        cashRegisters
            .Setup(c => c.GetByIdAsync(registerId, It.IsAny<Guid?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashRegisterDto?)null);

        var svc = CreateService(EnabledTestOptions(), client.Object, cashRegisters.Object);

        var result = await svc.VerifyAsync(
            new FiskalyVerifyTestRequest { CashRegisterId = registerId, ReceiptId = "42" },
            actorIsSuperAdmin: true);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public void GetScenarios_IncludesSignableAndAutoGenerated()
    {
        var svc = CreateService(EnabledTestOptions(), Mock.Of<IFiskalyClient>());
        var scenarios = svc.GetScenarios();

        Assert.Contains(scenarios, s => s.Id == FiskalySignTestScenarioIds.Normal && s.CanSign);
        Assert.Contains(scenarios, s => s.Id == FiskalySignTestScenarioIds.Cancellation && s.CanSign);
        Assert.Contains(scenarios, s => s.Id == FiskalySignTestScenarioIds.Training && s.CanSign);
        Assert.Contains(scenarios, s => s.Id == FiskalySignTestScenarioIds.MixedVat && s.CanSign);
        Assert.Contains(scenarios, s => s.Id == FiskalySignTestScenarioIds.ZeroAmount && s.CanSign);
        Assert.Contains(scenarios, s => s.Id == FiskalySignTestScenarioIds.Raw && s.CanSign);
        Assert.Contains(scenarios, s => s.Id == FiskalySignTestScenarioIds.MonthlyClose && !s.CanSign);
        Assert.Contains(scenarios, s => s.Id == FiskalySignTestScenarioIds.YearlyClose && !s.CanSign);
    }

    private static FiskalyOptions EnabledTestOptions() =>
        new()
        {
            Enabled = true,
            ApiKey = "test-key",
            ApiSecret = "test-secret",
            Environment = FiskalyOptions.TestEnvironment
        };

    private static FiskalySignTestService CreateService(
        FiskalyOptions options,
        IFiskalyClient client,
        ICashRegisterManagementService? cashRegisters = null)
    {
        var (_, cache) = CreateStore();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        return new FiskalySignTestService(
            Options.Create(options).ToMonitor(),
            cache,
            client,
            cashRegisters ?? Mock.Of<ICashRegisterManagementService>(),
            audit.Object,
            NullLogger<FiskalySignTestService>.Instance);
    }

    private static (IDbContextFactory<AppDbContext> Factory, FiskalyEnabledOverrideCache Cache) CreateStore()
    {
        var dbName = $"FiskalySignTest_{Guid.NewGuid():N}";
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() => CreateDb(dbName));
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => CreateDb(dbName));
        var cache = new FiskalyEnabledOverrideCache(
            factory.Object,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<FiskalyEnabledOverrideCache>.Instance);
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
