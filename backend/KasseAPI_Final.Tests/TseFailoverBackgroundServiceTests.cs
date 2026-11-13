using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseFailoverBackgroundServiceTests
{
    [Fact]
    public async Task RunCycleAsync_CallsCheckAndFailover_ForEachActivePrimary()
    {
        await using var db = CreateDb();
        var primaryA = await SeedPrimaryAsync(db, "A");
        var primaryB = await SeedPrimaryAsync(db, "B");
        // Backup / inactive primary must not be checked as a primary cycle target.
        await SeedBackupAsync(db, primaryA.Id, "BK");
        await SeedInactivePrimaryAsync(db, "X");

        var called = new List<Guid>();
        var failover = new Mock<ITseFailoverService>();
        failover
            .Setup(f => f.CheckAndFailoverAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
            {
                called.Add(id);
                return FailoverResult.Success("Primary is healthy, no failover needed", id);
            });

        var services = new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton(failover.Object)
            .AddSingleton<ILogger<TseFailoverBackgroundService>>(NullLogger<TseFailoverBackgroundService>.Instance)
            .BuildServiceProvider();

        await TseFailoverBackgroundService.RunCycleAsync(services, CancellationToken.None);

        Assert.Equal(2, called.Count);
        Assert.Contains(primaryA.Id, called);
        Assert.Contains(primaryB.Id, called);
        failover.Verify(
            f => f.CheckAndFailoverAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunCycleAsync_NeedsAttention_DoesNotThrow()
    {
        await using var db = CreateDb();
        var primary = await SeedPrimaryAsync(db, "ATT");

        var failover = new Mock<ITseFailoverService>();
        failover
            .Setup(f => f.CheckAndFailoverAsync(primary.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FailoverResult.Fail(
                "No healthy backup available",
                primary.Id,
                needsAttention: true));

        var services = new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton(failover.Object)
            .AddSingleton<ILogger<TseFailoverBackgroundService>>(NullLogger<TseFailoverBackgroundService>.Instance)
            .BuildServiceProvider();

        await TseFailoverBackgroundService.RunCycleAsync(services, CancellationToken.None);

        failover.Verify(
            f => f.CheckAndFailoverAsync(primary.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_failover_bg_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<TseDevice> SeedPrimaryAsync(AppDbContext db, string suffix)
    {
        var device = new TseDevice
        {
            SerialNumber = $"PRI-{suffix}-{Guid.NewGuid():N}"[..24],
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            KassenId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IsActive = true,
            IsPrimary = true,
            IsBackup = false,
            IsFailoverActive = false,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
        db.TseDevices.Add(device);
        await db.SaveChangesAsync();
        return device;
    }

    private static async Task SeedBackupAsync(AppDbContext db, Guid primaryId, string suffix)
    {
        db.TseDevices.Add(new TseDevice
        {
            SerialNumber = $"BKP-{suffix}-{Guid.NewGuid():N}"[..24],
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            KassenId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            PrimaryDeviceId = primaryId,
            IsActive = true,
            IsPrimary = false,
            IsBackup = true,
            IsFailoverActive = true,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedInactivePrimaryAsync(AppDbContext db, string suffix)
    {
        db.TseDevices.Add(new TseDevice
        {
            SerialNumber = $"INA-{suffix}-{Guid.NewGuid():N}"[..24],
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            KassenId = Guid.NewGuid(),
            IsActive = false,
            IsPrimary = true,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
