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

public sealed class TsePerformanceServiceTests
{
    [Fact]
    public async Task GetPerformanceMetricsAsync_AggregatesLatencyAndSuccess()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId) = await SeedDeviceAsync(db);
        var now = DateTime.UtcNow;

        db.TseDeviceHealthSamples.AddRange(
            Sample(deviceId, tenantId, now.AddHours(-3), 100, TseHealthStatus.Healthy, 120),
            Sample(deviceId, tenantId, now.AddHours(-2), 90, TseHealthStatus.Healthy, 180),
            Sample(deviceId, tenantId, now.AddHours(-1), 40, TseHealthStatus.Unhealthy, 4500),
            Sample(deviceId, tenantId, now.AddMinutes(-30), 95, TseHealthStatus.Healthy, null));
        await db.SaveChangesAsync();

        var activity = new Mock<IActivityEventPublisher>();
        var svc = CreateService(db, activity.Object);

        var metrics = await svc.GetPerformanceMetricsAsync(deviceId, now.AddDays(-1), now);

        Assert.Equal(4, metrics.TotalRequests);
        Assert.Equal(3, metrics.SuccessfulRequests);
        Assert.Equal(1, metrics.FailedRequests);
        Assert.Equal(3, metrics.TimedSamples);
        Assert.Equal(75.0, metrics.SuccessRate);
        Assert.Equal(25.0, metrics.ErrorRate);
        Assert.Equal(1600.0, metrics.AverageResponseTime); // (120+180+4500)/3
        Assert.Equal(120.0, metrics.MinResponseTime);
        Assert.Equal(4500.0, metrics.MaxResponseTime);
        Assert.Equal(4, metrics.PerformanceHistory.Count);
    }

    [Fact]
    public async Task CheckPerformanceAnomaliesAsync_PublishesSlowAndErrorAlerts()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId) = await SeedDeviceAsync(db);
        var now = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            db.TseDeviceHealthSamples.Add(
                Sample(deviceId, tenantId, now.AddMinutes(-(i + 1)), 20, TseHealthStatus.Offline, 12000));
        }

        await db.SaveChangesAsync();

        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = CreateService(db, activity.Object, new TseOptions
        {
            HealthSlowResponseMs = 3000,
            HealthCriticalResponseMs = 10000,
            HealthErrorRateWarningPercent = 20,
            HealthErrorRateCriticalPercent = 50,
            HealthPerformanceLookbackHours = 24,
            HealthSampleRetentionDays = 30,
        });

        var alert = await svc.CheckPerformanceAnomaliesAsync(deviceId);

        Assert.True(alert.HasAnomaly);
        Assert.Equal("Critical", alert.Severity);
        Assert.Contains(alert.Codes, c => c.Contains("slow", StringComparison.Ordinal));
        Assert.Contains(alert.Codes, c => c.Contains("error", StringComparison.Ordinal));
        Assert.True(alert.AlertPublished);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.TsePerformanceSlow,
                It.IsAny<object?>(),
                "system",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.TsePerformanceHighErrorRate,
                It.IsAny<object?>(),
                "system",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TsePerformanceService CreateService(
        AppDbContext db,
        IActivityEventPublisher activity,
        TseOptions? opts = null)
    {
        return new TsePerformanceService(
            db,
            Options.Create(opts ?? new TseOptions
            {
                HealthSlowResponseMs = 3000,
                HealthCriticalResponseMs = 10000,
                HealthErrorRateWarningPercent = 20,
                HealthErrorRateCriticalPercent = 50,
                HealthPerformanceLookbackHours = 24,
                HealthSampleRetentionDays = 30,
            }).ToMonitor(),
            activity,
            NullLogger<TsePerformanceService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_perf_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<(Guid TenantId, Guid DeviceId)> SeedDeviceAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Perf Cafe",
            Slug = "perf-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-P1",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        var device = new TseDevice
        {
            SerialNumber = "PERF-1",
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "perf-device",
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
        };
        db.TseDevices.Add(device);
        await db.SaveChangesAsync();
        return (tenantId, device.Id);
    }

    private static TseDeviceHealthSample Sample(
        Guid deviceId,
        Guid tenantId,
        DateTime at,
        int score,
        TseHealthStatus status,
        int? responseMs) =>
        new()
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            TenantId = tenantId,
            CheckedAtUtc = at,
            HealthScore = score,
            HealthStatus = status,
            Message = "test",
            IsPrimary = true,
            ResponseTimeMs = responseMs,
        };
}
