using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseDisasterRecoveryServiceTests
{
    [Fact]
    public async Task GenerateRunbookAsync_CreatesScenarioSteps()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithDevicesAsync(db);
        var svc = CreateService(db, Mock.Of<IActivityEventPublisher>(), MockHealth());

        var runbook = await svc.GenerateRunbookAsync(tenantId, TseDrScenarios.TseFailure, "sa-1");
        Assert.Equal(TseDrRunbookStatuses.Ready, runbook.Status);
        Assert.Equal(TseDrScenarios.TseFailure, runbook.Scenario);
        Assert.NotEmpty(runbook.Steps);
        Assert.Contains(runbook.Steps, s => s.Action == "SimulateFailoverPlan" && s.IsAutomated);
        Assert.Contains(runbook.Steps, s => s.Action == "ActivateBackupManual" && !s.IsAutomated);
    }

    [Fact]
    public async Task RunDrDrillAsync_SimulatesWithoutLiveFailover()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithDevicesAsync(db);
        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateService(db, activity.Object, MockHealth());
        var report = await svc.RunDrDrillAsync(tenantId, TseDrScenarios.TseFailure, "sa-1");

        Assert.True(report.Execution.SimulationOnly);
        Assert.True(report.Execution.Success);
        Assert.True(report.Execution.CompletedSteps >= 1);
        Assert.True(report.Execution.SkippedManualSteps >= 1);
        Assert.True(report.RunbookId != Guid.Empty);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.TseDrDrillCompleted,
                It.IsAny<object?>(),
                "sa-1",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDrStatusAsync_ReportsReadyWhenInventoryOk()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithDevicesAsync(db);
        var svc = CreateService(db, Mock.Of<IActivityEventPublisher>(), MockHealth());

        var status = await svc.GetDrStatusAsync(tenantId);
        Assert.True(status.IsReady);
        Assert.True(status.PrimaryDeviceCount >= 1);
        Assert.True(status.HealthyBackupCount >= 1);
        Assert.Equal(30, status.RtoTargetMinutes);
    }

    private static ITseDeviceHealthCheckService MockHealth()
    {
        var health = new Mock<ITseDeviceHealthCheckService>();
        health.Setup(h => h.CheckHealthAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new TseHealthResult
            {
                DeviceId = id,
                HealthScore = 95,
                Status = TseHealthStatus.Healthy,
                Message = "ok",
                CheckedAt = DateTime.UtcNow,
            });
        return health.Object;
    }

    private static TseDisasterRecoveryService CreateService(
        AppDbContext db,
        IActivityEventPublisher activity,
        ITseDeviceHealthCheckService health) =>
        new(
            db,
            Options.Create(new TseOptions
            {
                DrRtoTargetMinutes = 30,
                DrMinHealthyBackups = 1,
                DrMaxDrillAgeDays = 90,
            }).ToMonitor(),
            health,
            activity,
            NullLogger<TseDisasterRecoveryService>.Instance);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_dr_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<Guid> SeedTenantWithDevicesAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "DR Cafe",
            Slug = "dr-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-DR",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        db.TseDevices.Add(new TseDevice
        {
            SerialNumber = "DR-P1",
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "dr-primary",
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = true,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 100,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        });
        db.TseDevices.Add(new TseDevice
        {
            SerialNumber = "DR-B1",
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "dr-backup",
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = false,
            IsBackup = true,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 95,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }
}
