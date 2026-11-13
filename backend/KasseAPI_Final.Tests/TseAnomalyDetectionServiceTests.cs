using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseAnomalyDetectionServiceTests
{
    [Fact]
    public async Task DetectAnomaliesAsync_WithHealthyBaseline_ReturnsNoAnomalies()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId) = await SeedTenantDeviceAsync(db);
        SeedHealthySamples(db, tenantId, deviceId, score: 95, responseMs: 100);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.DetectAnomaliesAsync(tenantId, "admin");

        Assert.Equal(tenantId, result.TenantId);
        Assert.True(result.DiagnosticOnly);
        Assert.Empty(result.Anomalies);
        Assert.False(result.RequiresAction);
    }

    [Fact]
    public async Task DetectAnomaliesAsync_HealthScoreDrop_PersistsHighOrCritical()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId) = await SeedTenantDeviceAsync(db);
        var now = DateTime.UtcNow;

        // Baseline: healthy scores ~95 for 10 days (older than recent window)
        for (var i = 0; i < 20; i++)
        {
            db.TseDeviceHealthSamples.Add(new TseDeviceHealthSample
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                TenantId = tenantId,
                CheckedAtUtc = now.AddDays(-10).AddHours(i),
                HealthScore = 95,
                HealthStatus = TseHealthStatus.Healthy,
                ResponseTimeMs = 80,
                IsPrimary = true,
            });
        }

        // Recent: severe drop
        for (var i = 0; i < 4; i++)
        {
            db.TseDeviceHealthSamples.Add(new TseDeviceHealthSample
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                TenantId = tenantId,
                CheckedAtUtc = now.AddHours(-i),
                HealthScore = 20,
                HealthStatus = TseHealthStatus.Unhealthy,
                ResponseTimeMs = 80,
                IsPrimary = true,
            });
        }

        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.DetectAnomaliesAsync(tenantId, "admin");

        Assert.Contains(result.Anomalies, a => a.MetricName == TseAnomalyMetrics.HealthScore);
        Assert.True(TseAnomalySeverities.Rank(result.OverallSeverity) >= TseAnomalySeverities.Rank(TseAnomalySeverities.High));
        Assert.True(result.RequiresAction);
        Assert.True(await db.TseAnomalies.AnyAsync(a => a.TenantId == tenantId && !a.IsResolved));
    }

    [Fact]
    public async Task ResolveAnomalyAsync_MarksResolved()
    {
        await using var db = CreateDb();
        var (tenantId, _) = await SeedTenantDeviceAsync(db);
        var anomaly = new TseAnomaly
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MetricName = TseAnomalyMetrics.ResponseTimeMs,
            CurrentValue = 900,
            ExpectedValue = 100,
            DeviationPercent = 800,
            Severity = TseAnomalySeverities.Critical,
            Description = "slow",
            DetectedAt = DateTime.UtcNow,
        };
        db.TseAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        var resolved = await CreateService(db).ResolveAnomalyAsync(anomaly.Id, "admin");
        Assert.True(resolved.IsResolved);
        Assert.NotNull(resolved.ResolvedAt);
    }

    [Fact]
    public async Task GenerateAnomalyReportAsync_CountsBySeverity()
    {
        await using var db = CreateDb();
        var (tenantId, _) = await SeedTenantDeviceAsync(db);
        var now = DateTime.UtcNow;
        db.TseAnomalies.AddRange(
            new TseAnomaly
            {
                TenantId = tenantId,
                MetricName = "A",
                Severity = TseAnomalySeverities.Critical,
                Description = "c",
                DetectedAt = now.AddHours(-1),
                DeviationPercent = 90,
            },
            new TseAnomaly
            {
                TenantId = tenantId,
                MetricName = "B",
                Severity = TseAnomalySeverities.Low,
                Description = "l",
                DetectedAt = now.AddHours(-2),
                DeviationPercent = 20,
                IsResolved = true,
                ResolvedAt = now,
            });
        await db.SaveChangesAsync();

        var report = await CreateService(db).GenerateAnomalyReportAsync(
            tenantId,
            now.AddDays(-1),
            now.AddMinutes(1));

        Assert.Equal(2, report.TotalAnomalies);
        Assert.Equal(1, report.OpenAnomalies);
        Assert.Equal(1, report.CriticalCount);
        Assert.Equal(1, report.LowCount);
    }

    [Fact]
    public async Task IsAnomalyDetectedAsync_VolumeSpike_ReturnsTrue()
    {
        await using var db = CreateDb();
        var (tenantId, _) = await SeedTenantDeviceAsync(db);
        var today = DateTime.UtcNow.Date;
        for (var d = 1; d <= 10; d++)
        {
            for (var i = 0; i < 10; i++)
            {
                db.Receipts.Add(new Receipt
                {
                    ReceiptId = Guid.NewGuid(),
                    PaymentId = Guid.NewGuid(),
                    TenantId = tenantId,
                    CashRegisterId = Guid.NewGuid(),
                    ReceiptNumber = $"R-{d}-{i}",
                    IssuedAt = today.AddDays(-d).AddHours(1),
                    SignatureValue = "sig",
                    CreatedAt = today.AddDays(-d),
                    SubTotal = 1,
                    TaxTotal = 0,
                    GrandTotal = 1,
                });
            }
        }

        await db.SaveChangesAsync();
        var svc = CreateService(db);
        Assert.True(await svc.IsAnomalyDetectedAsync(
            tenantId,
            TseAnomalyMetrics.DailyTransactionVolume,
            value: 200));
        Assert.False(await svc.IsAnomalyDetectedAsync(
            tenantId,
            TseAnomalyMetrics.DailyTransactionVolume,
            value: 10));
    }

    private static TseAnomalyDetectionService CreateService(AppDbContext db) =>
        new(db, Mock.Of<IActivityEventPublisher>(), NullLogger<TseAnomalyDetectionService>.Instance);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_anomaly_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<(Guid TenantId, Guid DeviceId)> SeedTenantDeviceAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Anomaly Cafe",
            Slug = "anomaly-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var now = DateTime.UtcNow;
        db.TseDevices.Add(new TseDevice
        {
            Id = deviceId,
            TenantId = tenantId,
            SerialNumber = "ANOM-1",
            DeviceType = "fiskaly",
            Provider = "fiskaly",
            VendorId = "v",
            ProductId = "p",
            IsConnected = true,
            LastConnectionTime = now,
            LastSignatureTime = now,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            CanCreateInvoices = true,
            FinanzOnlineUsername = "fo",
            FinanzOnlineEnabled = false,
            LastFinanzOnlineSync = now,
            KassenId = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = now,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 95,
            IsPrimary = true,
            LastHealthCheck = now,
        });
        await db.SaveChangesAsync();
        return (tenantId, deviceId);
    }

    private static void SeedHealthySamples(AppDbContext db, Guid tenantId, Guid deviceId, int score, int responseMs)
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < 24; i++)
        {
            db.TseDeviceHealthSamples.Add(new TseDeviceHealthSample
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                TenantId = tenantId,
                CheckedAtUtc = now.AddDays(-7).AddHours(i),
                HealthScore = score,
                HealthStatus = TseHealthStatus.Healthy,
                ResponseTimeMs = responseMs,
                IsPrimary = true,
            });
            db.TseDeviceHealthSamples.Add(new TseDeviceHealthSample
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                TenantId = tenantId,
                CheckedAtUtc = now.AddHours(-i % 5),
                HealthScore = score,
                HealthStatus = TseHealthStatus.Healthy,
                ResponseTimeMs = responseMs,
                IsPrimary = true,
            });
        }
    }
}
