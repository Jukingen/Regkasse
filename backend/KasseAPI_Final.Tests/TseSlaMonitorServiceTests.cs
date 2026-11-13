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

public sealed class TseSlaMonitorServiceTests
{
    [Fact]
    public async Task GetSlaReportAsync_MeetsTargets_GradeA()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId, registerId) = await SeedAsync(db);
        var now = DateTime.UtcNow;

        db.TseDeviceHealthSamples.AddRange(
            Sample(deviceId, tenantId, now.AddHours(-3), TseHealthStatus.Healthy, 100),
            Sample(deviceId, tenantId, now.AddHours(-2), TseHealthStatus.Healthy, 150),
            Sample(deviceId, tenantId, now.AddHours(-1), TseHealthStatus.Degraded, 200));
        db.Receipts.AddRange(
            Receipt(tenantId, registerId, now.AddHours(-2), "sig-1"),
            Receipt(tenantId, registerId, now.AddHours(-1), "sig-2"));
        await db.SaveChangesAsync();

        var svc = CreateService(db, Mock.Of<IActivityEventPublisher>());
        var report = await svc.GetSlaReportAsync(tenantId, now.AddDays(-1), now);

        Assert.Equal(100.0, report.UptimePercentage);
        Assert.True(report.IsUptimeTargetMet);
        Assert.True(report.IsResponseTimeTargetMet);
        Assert.Equal(100.0, report.SuccessRate);
        Assert.True(report.IsSuccessRateTargetMet);
        Assert.Empty(report.Violations);
        Assert.Equal("A", report.Grade);
        Assert.Equal(150.0, report.AverageResponseTime);
    }

    [Fact]
    public async Task GetSlaReportAsync_DetectsViolations_AndLowersGrade()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId, registerId) = await SeedAsync(db);
        var now = DateTime.UtcNow;

        db.TseDeviceHealthSamples.AddRange(
            Sample(deviceId, tenantId, now.AddHours(-4), TseHealthStatus.Offline, 8000),
            Sample(deviceId, tenantId, now.AddHours(-3), TseHealthStatus.Unhealthy, 9000),
            Sample(deviceId, tenantId, now.AddHours(-2), TseHealthStatus.Healthy, 100),
            Sample(deviceId, tenantId, now.AddHours(-1), TseHealthStatus.Offline, 12000));
        db.Receipts.AddRange(
            Receipt(tenantId, registerId, now.AddHours(-2), "sig-ok"),
            Receipt(tenantId, registerId, now.AddHours(-1), null));
        await db.SaveChangesAsync();

        var svc = CreateService(db, Mock.Of<IActivityEventPublisher>(), new TseOptions
        {
            SlaTargetUptimePercent = 99.5,
            SlaTargetResponseTimeMs = 2000,
            SlaTargetSuccessRatePercent = 99.0,
            SlaStatusLookbackHours = 24,
            HealthSampleRetentionDays = 30,
        });

        var report = await svc.GetSlaReportAsync(tenantId, now.AddDays(-1), now);

        Assert.Equal(25.0, report.UptimePercentage);
        Assert.False(report.IsUptimeTargetMet);
        Assert.False(report.IsResponseTimeTargetMet);
        Assert.Equal(50.0, report.SuccessRate);
        Assert.False(report.IsSuccessRateTargetMet);
        Assert.Equal(3, report.Violations.Count);
        Assert.NotEqual("A", report.Grade);
    }

    [Fact]
    public async Task CheckSlaViolationsAsync_PublishesActivity()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId, _) = await SeedAsync(db);
        var now = DateTime.UtcNow;

        for (var i = 0; i < 4; i++)
        {
            db.TseDeviceHealthSamples.Add(
                Sample(deviceId, tenantId, now.AddMinutes(-(i + 1)), TseHealthStatus.Offline, 15000));
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
            SlaTargetUptimePercent = 99.5,
            SlaTargetResponseTimeMs = 2000,
            SlaTargetSuccessRatePercent = 99.0,
            SlaStatusLookbackHours = 24,
            HealthSampleRetentionDays = 30,
        });

        var alert = await svc.CheckSlaViolationsAsync(tenantId);
        Assert.True(alert.HasViolations);
        Assert.True(alert.AlertPublished);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.TseSlaViolation,
                It.IsAny<object?>(),
                "system",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TseSlaMonitorService CreateService(
        AppDbContext db,
        IActivityEventPublisher activity,
        TseOptions? opts = null)
    {
        return new TseSlaMonitorService(
            db,
            Options.Create(opts ?? new TseOptions
            {
                SlaTargetUptimePercent = 99.5,
                SlaTargetResponseTimeMs = 2000,
                SlaTargetSuccessRatePercent = 99.0,
                SlaStatusLookbackHours = 24,
                HealthSampleRetentionDays = 30,
            }).ToMonitor(),
            activity,
            NullLogger<TseSlaMonitorService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_sla_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<(Guid TenantId, Guid DeviceId, Guid RegisterId)> SeedAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "SLA Cafe",
            Slug = "sla-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-SLA",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        var device = new TseDevice
        {
            SerialNumber = "SLA-1",
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "sla-device",
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
        return (tenantId, device.Id, register.Id);
    }

    private static TseDeviceHealthSample Sample(
        Guid deviceId,
        Guid tenantId,
        DateTime at,
        TseHealthStatus status,
        int? responseMs) =>
        new()
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            TenantId = tenantId,
            CheckedAtUtc = at,
            HealthScore = status == TseHealthStatus.Healthy ? 100 : 20,
            HealthStatus = status,
            Message = "test",
            IsPrimary = true,
            ResponseTimeMs = responseMs,
        };

    private static Receipt Receipt(Guid tenantId, Guid registerId, DateTime issuedAt, string? signature) =>
        new()
        {
            ReceiptId = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentId = Guid.NewGuid(),
            ReceiptNumber = $"R-{Guid.NewGuid():N}"[..12],
            IssuedAt = issuedAt,
            CashRegisterId = registerId,
            SubTotal = 10m,
            TaxTotal = 2m,
            GrandTotal = 12m,
            SignatureValue = signature,
            CreatedAt = issuedAt,
        };
}
