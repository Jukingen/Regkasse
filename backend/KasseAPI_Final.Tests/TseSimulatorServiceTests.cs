using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseSimulatorServiceTests
{
    [Fact]
    public async Task SimulateTseFailureAsync_ConnectionLost_UpdatesDevice_InDevelopment()
    {
        await using var db = CreateDb();
        var device = await SeedDeviceAsync(db);

        var state = new TseSimulatorStateStore();
        var health = CreateHealth(db, state);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var svc = new TseSimulatorService(
            db,
            state,
            health,
            audit.Object,
            new FakeHostEnvironment(Environments.Development),
            NullLogger<TseSimulatorService>.Instance);

        var result = await svc.SimulateTseFailureAsync(
            device.Id,
            TseSimulatorFailureType.ConnectionLost,
            "tester");

        Assert.True(result.Success);
        Assert.Equal("failure.ConnectionLost", result.ScenarioId);

        var reloaded = await db.TseDevices.AsNoTracking().FirstAsync(d => d.Id == device.Id);
        Assert.False(reloaded.IsConnected);
        Assert.False(reloaded.CanCreateInvoices);
        Assert.Contains("connection lost", reloaded.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SimulateTseFailureAsync_DeniedOutsideDevelopment()
    {
        await using var db = CreateDb();
        var device = await SeedDeviceAsync(db);
        var state = new TseSimulatorStateStore();

        var svc = new TseSimulatorService(
            db,
            state,
            CreateHealth(db, state),
            Mock.Of<IAuditLogService>(),
            new FakeHostEnvironment(Environments.Production),
            NullLogger<TseSimulatorService>.Instance);

        var result = await svc.SimulateTseFailureAsync(
            device.Id,
            TseSimulatorFailureType.NetworkTimeout);

        Assert.False(result.Success);
        Assert.Contains("Development", result.Error ?? "", StringComparison.OrdinalIgnoreCase);

        var reloaded = await db.TseDevices.AsNoTracking().FirstAsync(d => d.Id == device.Id);
        Assert.True(reloaded.IsConnected);
    }

    [Fact]
    public async Task ResetSimulationAsync_RestoresBaseline()
    {
        await using var db = CreateDb();
        var device = await SeedDeviceAsync(db);
        var state = new TseSimulatorStateStore();
        var audit = Mock.Of<IAuditLogService>();

        var svc = new TseSimulatorService(
            db,
            state,
            CreateHealth(db, state),
            audit,
            new FakeHostEnvironment(Environments.Development),
            NullLogger<TseSimulatorService>.Instance);

        await svc.SimulateTseFailureAsync(device.Id, TseSimulatorFailureType.CertificateInvalid);
        var reset = await svc.ResetSimulationAsync(device.Id);

        Assert.True(reset.Success);
        var reloaded = await db.TseDevices.AsNoTracking().FirstAsync(d => d.Id == device.Id);
        Assert.Equal("VALID", reloaded.CertificateStatus);
        Assert.True(reloaded.IsConnected);
        Assert.Null(state.GetActiveScenarioId(device.Id));
    }

    [Fact]
    public async Task SimulateNetworkLatencyAsync_AppliesProbeDelay()
    {
        await using var db = CreateDb();
        var device = await SeedDeviceAsync(db);
        var state = new TseSimulatorStateStore();
        var svc = new TseSimulatorService(
            db,
            state,
            CreateHealth(db, state),
            Mock.Of<IAuditLogService>(),
            new FakeHostEnvironment(Environments.Development),
            NullLogger<TseSimulatorService>.Instance);

        var result = await svc.SimulateNetworkLatencyAsync(device.Id, 250);
        Assert.True(result.Success);
        Assert.Equal(250, state.GetLatencyMs(device.Id));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await CreateHealth(db, state).CheckHealthAsync(device.Id);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds >= 200, $"Expected delay, got {sw.ElapsedMilliseconds}ms");
    }

    private static TseDeviceHealthCheckService CreateHealth(AppDbContext db, ITseSimulatorStateStore state)
    {
        var provider = new Mock<ITseProvider>();
        provider.Setup(p => p.IsReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return new TseDeviceHealthCheckService(
            db,
            provider.Object,
            Options.Create(new TseOptions { TseMode = "Demo", Mode = "Fake" }).ToMonitor(),
            Mock.Of<ITseHealthTrendService>(),
            state,
            NullLogger<TseDeviceHealthCheckService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_sim_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<TseDevice> SeedDeviceAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Sim Cafe",
            Slug = "sim-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "K-SIM",
            Location = "Test",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        var device = new TseDevice
        {
            SerialNumber = "SIM-1",
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "sim-device",
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = true,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 100,
            ExpiresAt = DateTime.UtcNow.AddYears(1),
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
        db.TseDevices.Add(device);
        await db.SaveChangesAsync();
        return device;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
