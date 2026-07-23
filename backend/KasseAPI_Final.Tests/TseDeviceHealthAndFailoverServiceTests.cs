using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseDeviceHealthCheckServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_health_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TseDeviceHealthCheckService CreateService(
        AppDbContext db,
        bool providerReady = true,
        TseOptions? opts = null)
    {
        var provider = new Mock<ITseProvider>();
        provider.Setup(p => p.IsReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(providerReady);

        return new TseDeviceHealthCheckService(
            db,
            provider.Object,
            Options.Create(opts ?? new TseOptions { TseMode = "Demo", Mode = "Fake" }).ToMonitor(),
            Mock.Of<ITseHealthTrendService>(),
            new TseSimulatorStateStore(),
            NullLogger<TseDeviceHealthCheckService>.Instance);
    }

    [Fact]
    public async Task CheckHealthAsync_ConnectedReadyDevice_IsHealthy()
    {
        await using var db = CreateDb();
        var device = await SeedDeviceAsync(db, connected: true, canSign: true);

        var svc = CreateService(db);
        var result = await svc.CheckHealthAsync(device.Id);

        Assert.True(result.IsHealthy);
        Assert.Equal(TseHealthStatus.Healthy, result.Status);
        Assert.True(result.HealthScore >= 80);

        var reloaded = await db.TseDevices.FindAsync(device.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(TseHealthStatus.Healthy, reloaded!.HealthStatus);
        Assert.NotNull(reloaded.LastHealthCheck);
    }

    [Fact]
    public async Task CheckHealthAsync_ExpiredCertificate_ReturnsExpired()
    {
        await using var db = CreateDb();
        var device = await SeedDeviceAsync(db, connected: true, canSign: true);
        device.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.CheckHealthAsync(device.Id);

        Assert.False(result.IsHealthy);
        Assert.Equal(TseHealthStatus.Expired, result.Status);
    }

    [Fact]
    public async Task IsDeviceOperationalAsync_Disconnected_ReturnsFalse()
    {
        await using var db = CreateDb();
        var device = await SeedDeviceAsync(db, connected: false, canSign: false);

        var svc = CreateService(db);
        Assert.False(await svc.IsDeviceOperationalAsync(device.Id));
    }

    [Theory]
    [InlineData(100, TseHealthStatus.Healthy)]
    [InlineData(80, TseHealthStatus.Healthy)]
    [InlineData(60, TseHealthStatus.Degraded)]
    [InlineData(40, TseHealthStatus.Unhealthy)]
    [InlineData(0, TseHealthStatus.Offline)]
    public void MapHealthStatus_UsesThresholds(int score, TseHealthStatus expected)
    {
        Assert.Equal(expected, TseDeviceHealthCheckService.MapHealthStatus(score));
    }

    private static async Task<TseDevice> SeedDeviceAsync(AppDbContext db, bool connected, bool canSign)
    {
        var device = new TseDevice
        {
            SerialNumber = $"SER-{Guid.NewGuid():N}"[..20],
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            KassenId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IsConnected = connected,
            CanCreateInvoices = canSign,
            IsActive = true,
            IsPrimary = true,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
        db.TseDevices.Add(device);
        await db.SaveChangesAsync();
        return device;
    }
}

public sealed class TseFailoverServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_failover_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static (
        TseFailoverService Svc,
        Mock<ITseDeviceHealthCheckService> Health,
        Mock<ITseFailoverNotificationService> Notifications) CreateService(
        AppDbContext db,
        bool isSuperAdmin = true)
    {
        var health = new Mock<ITseDeviceHealthCheckService>();
        var audit = new Mock<IAuditLogService>();
        var notifications = new Mock<ITseFailoverNotificationService>();
        notifications
            .Setup(n => n.NotifyFailoverStartedAsync(
                It.IsAny<TseDevice>(), It.IsAny<TseDevice>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notifications
            .Setup(n => n.NotifyFailoverCompletedAsync(
                It.IsAny<TseDevice>(), It.IsAny<TseDevice>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notifications
            .Setup(n => n.NotifyFailoverFailedAsync(
                It.IsAny<TseDevice>(),
                It.IsAny<TseDevice?>(),
                It.IsAny<Exception?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notifications
            .Setup(n => n.NotifyNoBackupAvailableAsync(
                It.IsAny<TseDevice>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notifications
            .Setup(n => n.NotifyBackupLowHealthAsync(It.IsAny<TseDevice>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notifications
            .Setup(n => n.NotifyFailoverRevertedAsync(It.IsAny<TseDevice>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
        userManager
            .Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ApplicationUser
            {
                Id = id,
                UserName = "sa",
                Role = isSuperAdmin ? Roles.SuperAdmin : Roles.Cashier,
            });
        userManager
            .Setup(m => m.IsInRoleAsync(It.IsAny<ApplicationUser>(), Roles.SuperAdmin))
            .ReturnsAsync(isSuperAdmin);

        var svc = new TseFailoverService(
            db,
            health.Object,
            audit.Object,
            notifications.Object,
            userManager.Object,
            Options.Create(new TseOptions { AutoFailoverEnabled = true }).ToMonitor(),
            NullLogger<TseFailoverService>.Instance);

        return (svc, health, notifications);
    }

    [Fact]
    public async Task CheckAndFailoverAsync_HealthyPrimary_NoFailover()
    {
        await using var db = CreateDb();
        var (tenantId, primaryId, _) = await SeedPrimaryBackupAsync(db);
        var (svc, health, _) = CreateService(db);
        health
            .Setup(h => h.CheckHealthAsync(primaryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseHealthResult
            {
                DeviceId = primaryId,
                IsHealthy = true,
                HealthScore = 100,
                Status = TseHealthStatus.Healthy,
                Message = "OK",
            });

        var result = await svc.CheckAndFailoverAsync(primaryId);

        Assert.True(result.Succeeded);
        Assert.Contains("no failover", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.TseFailoverLogs.Where(l => l.TenantId == tenantId).ToListAsync());
    }

    [Fact]
    public async Task CheckAndFailoverAsync_UnhealthyPrimary_ActivatesBackup()
    {
        await using var db = CreateDb();
        var (tenantId, primaryId, backupId) = await SeedPrimaryBackupAsync(db);
        var (svc, health, notifications) = CreateService(db);

        health
            .Setup(h => h.CheckHealthAsync(primaryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseHealthResult
            {
                DeviceId = primaryId,
                IsHealthy = false,
                HealthScore = 0,
                Status = TseHealthStatus.Offline,
                Message = "Not connected",
            });
        health
            .Setup(h => h.CheckHealthAsync(backupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseHealthResult
            {
                DeviceId = backupId,
                IsHealthy = true,
                HealthScore = 95,
                Status = TseHealthStatus.Healthy,
                Message = "OK",
            });

        var result = await svc.CheckAndFailoverAsync(primaryId);

        Assert.True(result.Succeeded);
        Assert.Equal(backupId, result.BackupDeviceId);

        var backup = await db.TseDevices.FindAsync(backupId);
        var primary = await db.TseDevices.FindAsync(primaryId);
        Assert.True(backup!.IsFailoverActive);
        Assert.False(primary!.IsFailoverActive);
        Assert.Equal(1, primary.FailoverCount);

        var log = await db.TseFailoverLogs.IgnoreQueryFilters().SingleAsync(l => l.TenantId == tenantId);
        Assert.True(log.IsSuccessful);
        Assert.Equal(TseFailoverTypes.Automatic, log.FailoverType);

        var settings = await db.CompanySettings.IgnoreQueryFilters().SingleAsync(s => s.TenantId == tenantId);
        Assert.Equal(backupId.ToString("D"), settings.DefaultTseDeviceId);

        notifications.Verify(
            n => n.NotifyFailoverStartedAsync(
                It.Is<TseDevice>(d => d.Id == primaryId),
                It.Is<TseDevice>(d => d.Id == backupId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        notifications.Verify(
            n => n.NotifyFailoverCompletedAsync(
                It.Is<TseDevice>(d => d.Id == primaryId),
                It.Is<TseDevice>(d => d.Id == backupId),
                TseFailoverTypes.Automatic,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckAndFailoverAsync_NoHealthyBackup_Notifies()
    {
        await using var db = CreateDb();
        var (_, primaryId, backupId) = await SeedPrimaryBackupAsync(db);
        var backup = await db.TseDevices.FindAsync(backupId);
        backup!.HealthStatus = TseHealthStatus.Unhealthy;
        backup.HealthScore = 10;
        await db.SaveChangesAsync();

        var (svc, health, notifications) = CreateService(db);
        health
            .Setup(h => h.CheckHealthAsync(primaryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseHealthResult
            {
                DeviceId = primaryId,
                IsHealthy = false,
                HealthScore = 0,
                Status = TseHealthStatus.Offline,
                Message = "Primary down",
            });

        var result = await svc.CheckAndFailoverAsync(primaryId);

        Assert.False(result.Succeeded);
        Assert.True(result.NeedsAttention);
        notifications.Verify(
            n => n.NotifyNoBackupAvailableAsync(
                It.Is<TseDevice>(d => d.Id == primaryId),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        notifications.Verify(
            n => n.NotifyBackupLowHealthAsync(
                It.Is<TseDevice>(d => d.Id == backupId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ManualFailoverAsync_NonSuperAdmin_Fails()
    {
        await using var db = CreateDb();
        var (_, primaryId, backupId) = await SeedPrimaryBackupAsync(db);
        var (svc, _, _) = CreateService(db, isSuperAdmin: false);

        var result = await svc.ManualFailoverAsync(primaryId, backupId, "user-1");

        Assert.False(result.Succeeded);
        Assert.Contains("SuperAdmin", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetActiveDeviceForRegisterAsync_ReturnsFailoverBackup()
    {
        await using var db = CreateDb();
        var (_, primaryId, backupId) = await SeedPrimaryBackupAsync(db);
        var registerId = (await db.TseDevices.FindAsync(primaryId))!.CashRegisterId!.Value;

        var backup = await db.TseDevices.FindAsync(backupId);
        backup!.IsFailoverActive = true;
        await db.SaveChangesAsync();

        var (svc, _, _) = CreateService(db);
        var active = await svc.GetActiveDeviceForRegisterAsync(registerId);

        Assert.NotNull(active);
        Assert.Equal(backupId, active!.Id);
    }

    private static async Task<(Guid TenantId, Guid PrimaryId, Guid BackupId)> SeedPrimaryBackupAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Failover Cafe",
            Slug = "failover-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-F1",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = tenantId,
            CompanyName = "Failover Cafe",
            CompanyAddress = "Teststrasse 1",
            CompanyTaxNumber = "ATU12345678",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var primary = new TseDevice
        {
            SerialNumber = $"PRI-{Guid.NewGuid():N}"[..20],
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            IsConnected = false,
            CanCreateInvoices = false,
            IsActive = true,
            IsPrimary = true,
            IsBackup = false,
            HealthStatus = TseHealthStatus.Offline,
            HealthScore = 0,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
        db.TseDevices.Add(primary);
        await db.SaveChangesAsync();

        var backup = new TseDevice
        {
            SerialNumber = $"BKP-{Guid.NewGuid():N}"[..20],
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            PrimaryDeviceId = primary.Id,
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = false,
            IsBackup = true,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 100,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
        db.TseDevices.Add(backup);

        // Bind default to primary initially
        var settings = await db.CompanySettings.IgnoreQueryFilters().SingleAsync(s => s.TenantId == tenantId);
        settings.DefaultTseDeviceId = primary.Id.ToString("D");
        await db.SaveChangesAsync();

        return (tenantId, primary.Id, backup.Id);
    }
}
