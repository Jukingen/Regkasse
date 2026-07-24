using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseAutoScalingServiceTests
{
    [Fact]
    public async Task ConfigureScalingPolicyAsync_PersistsClampedValues()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var svc = CreateService(db, isDevelopment: true);

        var policy = await svc.ConfigureScalingPolicyAsync(
            tenantId,
            new ConfigureTseScalingPolicyRequestDto
            {
                Enabled = true,
                MinDevices = 1,
                MaxDevices = 4,
                TargetTransactionsPerDevice = 1000,
                ScaleUpThreshold = 80,
                ScaleDownThreshold = 30,
                CooldownMinutes = 30,
                AutoProvision = false,
            },
            "admin");

        Assert.True(policy.Enabled);
        Assert.Equal(4, policy.MaxDevices);
        Assert.False(policy.AutoProvision);
    }

    [Fact]
    public async Task EvaluateAndScaleAsync_WhenDisabled_RecordsDisabled()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithDeviceAsync(db);
        var svc = CreateService(db, isDevelopment: true);

        var result = await svc.EvaluateAndScaleAsync(tenantId, "admin");

        Assert.Equal(TseScalingActions.Disabled, result.Action);
        Assert.False(result.Applied);
        Assert.True(result.SimulationOnly);
        Assert.Equal(1, await db.TseScalingHistory.CountAsync());
    }

    [Fact]
    public async Task EvaluateAndScaleAsync_HighLoad_RecommendsScaleUp_WithoutProvisioning()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithDeviceAsync(db);
        var now = DateTime.UtcNow;
        for (var i = 0; i < 800; i++)
        {
            db.Receipts.Add(new Receipt
            {
                ReceiptId = Guid.NewGuid(),
                PaymentId = Guid.NewGuid(),
                TenantId = tenantId,
                CashRegisterId = Guid.NewGuid(),
                ReceiptNumber = $"R-{i}",
                IssuedAt = now.AddHours(-(i % 24)),
                SignatureValue = "sig",
                CreatedAt = now,
                SubTotal = 1,
                TaxTotal = 0,
                GrandTotal = 1,
            });
        }

        await db.SaveChangesAsync();

        var svc = CreateService(db, isDevelopment: true);
        await svc.ConfigureScalingPolicyAsync(
            tenantId,
            new ConfigureTseScalingPolicyRequestDto
            {
                Enabled = true,
                MinDevices = 1,
                MaxDevices = 5,
                TargetTransactionsPerDevice = 100,
                ScaleUpThreshold = 50,
                ScaleDownThreshold = 10,
                CooldownMinutes = 30,
                AutoProvision = false,
            });

        var result = await svc.EvaluateAndScaleAsync(tenantId, "admin");

        Assert.True(result.RecommendedDevices > result.CurrentDevices);
        Assert.Equal(TseScalingActions.Recommend, result.Action);
        Assert.False(result.Applied);
        Assert.True(result.SimulationOnly);
        Assert.Equal(1, await db.TseDevices.CountAsync(d => d.TenantId == tenantId && d.IsActive));
    }

    [Fact]
    public async Task GetScalingHistoryAsync_ReturnsNewestFirst()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        db.TseScalingHistory.AddRange(
            new TseScalingHistoryEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EvaluatedAt = DateTime.UtcNow.AddHours(-2),
                Action = TseScalingActions.NoOp,
                FromDevices = 1,
                ToDevices = 1,
                Reason = "old",
            },
            new TseScalingHistoryEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EvaluatedAt = DateTime.UtcNow.AddMinutes(-5),
                Action = TseScalingActions.Recommend,
                FromDevices = 1,
                ToDevices = 2,
                Reason = "new",
            });
        await db.SaveChangesAsync();

        var history = await CreateService(db, true).GetScalingHistoryAsync(tenantId);
        Assert.Equal(2, history.Items.Count);
        Assert.Equal("new", history.Items[0].Reason);
    }

    private static TseAutoScalingService CreateService(AppDbContext db, bool isDevelopment)
    {
        var monitor = new Mock<IOptionsMonitor<TseOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(new TseOptions
        {
            CapacityPerDevicePerDay = 5000,
            CapacityWarningUtilizationPercent = 80,
        });

        return new TseAutoScalingService(
            db,
            monitor.Object,
            Mock.Of<IActivityEventPublisher>(),
            new AutoScaleFakeHostEnvironment(isDevelopment ? Environments.Development : Environments.Production),
            NullLogger<TseAutoScalingService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_autoscale_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<Guid> SeedTenantAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Scale Cafe",
            Slug = "scale-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static async Task<Guid> SeedTenantWithDeviceAsync(AppDbContext db)
    {
        var tenantId = await SeedTenantAsync(db);
        var now = DateTime.UtcNow;
        db.TseDevices.Add(new TseDevice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SerialNumber = "PRI-1",
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
            HealthScore = 100,
            IsPrimary = true,
            LastHealthCheck = now,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private sealed class AutoScaleFakeHostEnvironment : IHostEnvironment
    {
        public AutoScaleFakeHostEnvironment(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
