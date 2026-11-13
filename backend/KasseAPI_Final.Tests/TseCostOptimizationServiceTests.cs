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

public sealed class TseCostOptimizationServiceTests
{
    [Fact]
    public async Task GetCostReportAsync_AggregatesSigningAndDeviceFees()
    {
        await using var db = CreateDb();
        var (tenantId, registerId) = await SeedTenantWithPrimaryAsync(db);
        var now = DateTime.UtcNow;

        db.Receipts.AddRange(
            Receipt(tenantId, registerId, now.AddDays(-2), "sig-a"),
            Receipt(tenantId, registerId, now.AddDays(-1), "sig-b"),
            Receipt(tenantId, registerId, now.AddHours(-3), ""));
        await db.SaveChangesAsync();

        var svc = CreateService(db, Mock.Of<IActivityEventPublisher>(), new TseOptions
        {
            CostPerSignedTransactionEur = 0.01m,
            CostMonthlyPrimaryDeviceEur = 30m,
            CostMonthlyBackupDeviceEur = 5m,
            CostPerFailoverEventEur = 0m,
        });

        var from = now.AddDays(-30);
        var report = await svc.GetCostReportAsync(tenantId, from, now);

        Assert.Equal(3, report.TotalTransactions);
        Assert.Equal(2, report.SignedTransactions);
        Assert.Equal(1, report.ActiveDeviceCount);
        Assert.True(report.CostBreakdown.ContainsKey("signing"));
        Assert.Equal(0.02m, report.CostBreakdown["signing"]);
        Assert.True(report.TotalCost >= 0.02m);
        Assert.NotEmpty(report.Recommendations);
        Assert.NotEmpty(report.DailyTrends);
    }

    [Fact]
    public async Task GetOptimizationRecommendationsAsync_FlagsExcessBackups()
    {
        await using var db = CreateDb();
        var (tenantId, registerId) = await SeedTenantWithPrimaryAsync(db);

        db.TseDevices.AddRange(
            Backup(tenantId, registerId, "B1"),
            Backup(tenantId, registerId, "B2"));
        await db.SaveChangesAsync();

        var svc = CreateService(db, Mock.Of<IActivityEventPublisher>());
        var recommendations = await svc.GetOptimizationRecommendationsAsync(tenantId);

        Assert.Contains(recommendations, r => r.Code == "reduce_idle_backups");
        Assert.Contains(recommendations, r => r.EstimatedMonthlySavings > 0);
    }

    [Fact]
    public async Task CheckCostAnomaliesAsync_PublishesWhenPeriodCostSpikes()
    {
        await using var db = CreateDb();
        var (tenantId, registerId) = await SeedTenantWithPrimaryAsync(db);
        var now = DateTime.UtcNow;

        // Baseline window (~60–30 days ago): few receipts
        for (var i = 0; i < 2; i++)
        {
            db.Receipts.Add(Receipt(tenantId, registerId, now.AddDays(-(45 + i)), "sig-old"));
        }

        // Current window (last 30 days): many receipts → cost spike
        for (var i = 0; i < 40; i++)
        {
            db.Receipts.Add(Receipt(tenantId, registerId, now.AddDays(-(i + 1)), $"sig-{i}"));
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
            CostPerSignedTransactionEur = 1m,
            CostMonthlyPrimaryDeviceEur = 0m,
            CostMonthlyBackupDeviceEur = 0m,
            CostPerFailoverEventEur = 0m,
            CostAnomalyWarningIncreasePercent = 40,
            CostAnomalyCriticalIncreasePercent = 100,
            CostDailySpikeMultiplier = 10,
        });

        var alert = await svc.CheckCostAnomaliesAsync(tenantId);

        Assert.True(alert.HasAnomaly);
        Assert.Contains(alert.Codes, c => c.StartsWith("cost_spike", StringComparison.Ordinal));
        Assert.True(alert.AlertPublished);
        Assert.True(alert.CurrentPeriodCost > alert.BaselinePeriodCost);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.TseCostAnomaly,
                It.IsAny<object?>(),
                "system",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TseCostOptimizationService CreateService(
        AppDbContext db,
        IActivityEventPublisher activity,
        TseOptions? opts = null)
    {
        return new TseCostOptimizationService(
            db,
            Options.Create(opts ?? new TseOptions
            {
                CostPerSignedTransactionEur = 0.002m,
                CostMonthlyPrimaryDeviceEur = 15m,
                CostMonthlyBackupDeviceEur = 5m,
                CostPerFailoverEventEur = 2m,
                CostAnomalyWarningIncreasePercent = 40,
                CostAnomalyCriticalIncreasePercent = 100,
                CostDailySpikeMultiplier = 2.5,
                CostLowUtilizationDailyTxThreshold = 20,
                CostHighFailoverCountThreshold = 3,
            }).ToMonitor(),
            activity,
            NullLogger<TseCostOptimizationService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_cost_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<(Guid TenantId, Guid RegisterId)> SeedTenantWithPrimaryAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cost Cafe",
            Slug = "cost-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-C1",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        db.TseDevices.Add(new TseDevice
        {
            SerialNumber = "COST-P1",
            DeviceType = "Cloud",
            VendorId = "auto",
            ProductId = "fiskaly",
            Provider = TseOptions.ProviderFiskaly,
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "cost-primary",
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

    private static TseDevice Backup(Guid tenantId, Guid registerId, string serial) =>
        new()
        {
            SerialNumber = serial,
            DeviceType = "Cloud",
            VendorId = "auto",
            ProductId = "fiskaly",
            Provider = TseOptions.ProviderFiskaly,
            TenantId = tenantId,
            CashRegisterId = registerId,
            KassenId = registerId,
            DeviceId = $"backup-{serial}",
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

    private static Receipt Receipt(Guid tenantId, Guid registerId, DateTime issuedAt, string signature) =>
        new()
        {
            ReceiptId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            ReceiptNumber = Guid.NewGuid().ToString("N")[..12],
            IssuedAt = issuedAt,
            SubTotal = 10m,
            TaxTotal = 2m,
            GrandTotal = 12m,
            SignatureValue = signature,
            CreatedAt = issuedAt,
        };
}
