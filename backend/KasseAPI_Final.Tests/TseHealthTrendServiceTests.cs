using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseHealthTrendServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_health_trend_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TseHealthTrendService CreateService(AppDbContext db, TseOptions? opts = null)
    {
        return new TseHealthTrendService(
            db,
            Options.Create(opts ?? new TseOptions
            {
                FailoverHealthyMinScore = 80,
                FailoverDegradedMinScore = 50,
                HealthSampleMinIntervalSeconds = 60,
                HealthSampleRetentionDays = 30,
            }).ToMonitor(),
            NullLogger<TseHealthTrendService>.Instance);
    }

    private static async Task<(Guid TenantId, Guid PrimaryId, Guid BackupId)> SeedAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Trend Cafe",
            Slug = "trend-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-T1",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        var primary = new TseDevice
        {
            SerialNumber = "PRI-TREND",
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "primary-trend",
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = true,
            HealthStatus = TseHealthStatus.Degraded,
            HealthScore = 60,
            HealthMessage = "Degraded",
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
        db.TseDevices.Add(primary);
        await db.SaveChangesAsync();

        var backup = new TseDevice
        {
            SerialNumber = "BKP-TREND",
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "backup-trend",
            PrimaryDeviceId = primary.Id,
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
        };
        db.TseDevices.Add(backup);
        await db.SaveChangesAsync();
        return (tenantId, primary.Id, backup.Id);
    }

    [Fact]
    public async Task GenerateHealthReportAsync_SummarizesFleet_AndRecommends()
    {
        await using var db = CreateDb();
        var (tenantId, primaryId, _) = await SeedAsync(db);
        var svc = CreateService(db);

        var report = await svc.GenerateHealthReportAsync(tenantId);

        Assert.Equal(2, report.TotalDevices);
        Assert.Equal(1, report.HealthyDevices);
        Assert.Equal(1, report.DegradedDevices);
        Assert.Equal(0, report.UnhealthyDevices);
        Assert.True(report.AverageHealthScore > 0);
        Assert.Contains(report.Recommendations, r => r.Code == "PRIMARY_DEGRADED" && r.DeviceId == primaryId);
        Assert.Equal(80, report.HealthyMinScore);
        Assert.Equal(50, report.DegradedMinScore);
    }

    [Fact]
    public async Task TryRecordSampleAsync_AndGetHealthTrendAsync_ReturnsPoints()
    {
        await using var db = CreateDb();
        var (tenantId, primaryId, _) = await SeedAsync(db);
        var primary = await db.TseDevices.FindAsync(primaryId);
        var svc = CreateService(db);

        await svc.TryRecordSampleAsync(
            primary!,
            healthScore: 60,
            TseHealthStatus.Degraded,
            "Degraded",
            DateTime.UtcNow.AddHours(-2));
        await svc.TryRecordSampleAsync(
            primary!,
            healthScore: 90,
            TseHealthStatus.Healthy,
            "Recovered",
            DateTime.UtcNow);

        var trend = await svc.GetHealthTrendAsync(tenantId, days: 7, deviceId: primaryId);
        Assert.Equal(2, trend.Count);
        Assert.Equal(60, trend[0].Score);
        Assert.Equal(90, trend[1].Score);
        Assert.All(trend, p => Assert.Equal(primaryId, p.DeviceId));
    }

    [Fact]
    public async Task TryRecordSampleAsync_ThrottlesUnchangedScores()
    {
        await using var db = CreateDb();
        var (tenantId, primaryId, _) = await SeedAsync(db);
        var primary = await db.TseDevices.FindAsync(primaryId);
        var svc = CreateService(db, new TseOptions { HealthSampleMinIntervalSeconds = 3600 });

        var t0 = DateTime.UtcNow.AddMinutes(-10);
        await svc.TryRecordSampleAsync(primary!, 70, TseHealthStatus.Degraded, "a", t0);
        await svc.TryRecordSampleAsync(primary!, 70, TseHealthStatus.Degraded, "b", t0.AddMinutes(5));

        var count = await db.TseDeviceHealthSamples.CountAsync(s => s.DeviceId == primaryId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GenerateHealthReportAsync_IncludesActivityAlerts()
    {
        await using var db = CreateDb();
        var (tenantId, _, _) = await SeedAsync(db);
        db.ActivityEvents.Add(new ActivityEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Type = ActivityEventType.TseFailoverNoBackup,
            Severity = ActivitySeverityNames.Critical,
            Title = "No backup",
            Description = "Primary unhealthy",
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var report = await CreateService(db).GenerateHealthReportAsync(tenantId);
        Assert.Contains(report.RecentAlerts, a => a.Type == nameof(ActivityEventType.TseFailoverNoBackup));
    }
}
