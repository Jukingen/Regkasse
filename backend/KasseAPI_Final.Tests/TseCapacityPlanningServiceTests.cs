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

public sealed class TseCapacityPlanningServiceTests
{
    [Fact]
    public async Task GetCapacityReportAsync_BuildsDailyTrendsAndUtilization()
    {
        await using var db = CreateDb();
        var (tenantId, registerId) = await SeedAsync(db);
        var now = DateTime.UtcNow;

        for (var i = 0; i < 10; i++)
            db.Receipts.Add(Receipt(tenantId, registerId, now.Date.AddDays(-5).AddHours(10 + i), "sig"));
        for (var i = 0; i < 5; i++)
            db.Receipts.Add(Receipt(tenantId, registerId, now.Date.AddDays(-1).AddHours(12 + i), "sig"));
        await db.SaveChangesAsync();

        var svc = CreateService(db, Mock.Of<IActivityEventPublisher>(), new TseOptions
        {
            CapacityLookbackDays = 7,
            CapacityPerDevicePerDay = 20,
            CapacityPerDevicePerHour = 8,
            CapacityWarningUtilizationPercent = 80,
            CapacityCriticalUtilizationPercent = 95,
        });

        var report = await svc.GetCapacityReportAsync(tenantId);
        Assert.Equal(15, report.MonthlyTransactionTotal);
        Assert.Equal(7, report.LookbackDays);
        Assert.Equal(7, report.DailyTrends.Count);
        Assert.True(report.DailyTrends.Sum(t => t.TransactionCount) >= 15);
        Assert.Equal(1, report.ActiveSigningDevices);
        Assert.Equal(20, report.MaxDailyCapacity);
        Assert.NotEmpty(report.Recommendations);
    }

    [Fact]
    public async Task ForecastCapacityAsync_ReturnsProjectedDays()
    {
        await using var db = CreateDb();
        var (tenantId, registerId) = await SeedAsync(db);
        var now = DateTime.UtcNow;
        for (var d = 14; d >= 1; d--)
        {
            for (var i = 0; i < (15 - d); i++)
                db.Receipts.Add(Receipt(tenantId, registerId, now.Date.AddDays(-d).AddHours(10), "sig"));
        }

        await db.SaveChangesAsync();
        var svc = CreateService(db, Mock.Of<IActivityEventPublisher>());
        var forecast = await svc.ForecastCapacityAsync(tenantId, forecastDays: 10);
        Assert.Equal(10, forecast.ForecastDays);
        Assert.Equal(10, forecast.DailyPoints.Count);
        Assert.True(forecast.EstimatedTotalTransactions >= 0);
    }

    [Fact]
    public async Task CheckCapacityAlertsAsync_PublishesWhenNearLimit()
    {
        await using var db = CreateDb();
        var (tenantId, registerId) = await SeedAsync(db);
        var now = DateTime.UtcNow;

        // Force high daily average vs tiny capacity.
        for (var d = 0; d < 7; d++)
        {
            for (var i = 0; i < 18; i++)
                db.Receipts.Add(Receipt(tenantId, registerId, now.Date.AddDays(-d).AddHours(9), "sig"));
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
            CapacityLookbackDays = 7,
            CapacityPerDevicePerDay = 20,
            CapacityPerDevicePerHour = 50,
            CapacityWarningUtilizationPercent = 80,
            CapacityCriticalUtilizationPercent = 95,
            CapacityReachAlertDays = 60,
        });

        var alert = await svc.CheckCapacityAlertsAsync(tenantId);
        Assert.True(alert.HasAlert);
        Assert.True(alert.IsNearCapacity);
        Assert.True(alert.AlertPublished);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.TseCapacityNearLimit,
                It.IsAny<object?>(),
                "system",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TseCapacityPlanningService CreateService(
        AppDbContext db,
        IActivityEventPublisher activity,
        TseOptions? opts = null) =>
        new(
            db,
            Options.Create(opts ?? new TseOptions
            {
                CapacityLookbackDays = 30,
                CapacityPerDevicePerDay = 5000,
                CapacityPerDevicePerHour = 400,
                CapacityWarningUtilizationPercent = 80,
                CapacityCriticalUtilizationPercent = 95,
                CapacityReachAlertDays = 60,
            }).ToMonitor(),
            activity,
            NullLogger<TseCapacityPlanningService>.Instance);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_capacity_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<(Guid TenantId, Guid RegisterId)> SeedAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Capacity Cafe",
            Slug = "capacity-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-CAP",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        db.TseDevices.Add(new TseDevice
        {
            SerialNumber = "CAP-1",
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "cap-device",
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
        await db.SaveChangesAsync();
        return (tenantId, register.Id);
    }

    private static Receipt Receipt(Guid tenantId, Guid registerId, DateTime issuedAt, string? signature) =>
        new()
        {
            ReceiptId = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentId = Guid.NewGuid(),
            ReceiptNumber = Guid.NewGuid().ToString("N")[..12],
            IssuedAt = DateTime.SpecifyKind(issuedAt, DateTimeKind.Utc),
            CashRegisterId = registerId,
            SubTotal = 10m,
            TaxTotal = 2m,
            GrandTotal = 12m,
            SignatureValue = signature,
            CreatedAt = issuedAt,
        };
}
