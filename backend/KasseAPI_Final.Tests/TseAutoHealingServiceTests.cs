using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
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

public sealed class TseAutoHealingServiceTests
{
    [Fact]
    public async Task ConfigureHealingAsync_PersistsClampedValuesAndRules()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var svc = CreateService(db);

        var config = await svc.ConfigureHealingAsync(
            tenantId,
            new ConfigureTseHealingRequestDto
            {
                Enabled = true,
                MaxAutoHealAttempts = 99,
                CooldownMinutes = 0,
                NotifyOnHeal = false,
                AllowAutoFailover = false,
                Rules =
                [
                    new ConfigureTseHealingRuleDto
                    {
                        Condition = TseHealingConditions.DeviceOffline,
                        Action = TseHealingActions.ReprobeHealth,
                        Priority = 5,
                        Status = TseHealingRuleStatuses.Enabled,
                    },
                ],
            },
            "admin");

        Assert.True(config.Enabled);
        Assert.Equal(20, config.MaxAutoHealAttempts);
        Assert.Equal(1, config.CooldownMinutes);
        Assert.False(config.NotifyOnHeal);
        Assert.Single(config.Rules);
        Assert.Equal(TseHealingConditions.DeviceOffline, config.Rules[0].Condition);
    }

    [Fact]
    public async Task DiagnoseAndHealAsync_WhenDisabled_RecordsDiagnosedOnly()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId) = await SeedOfflineDeviceAsync(db);
        var health = new Mock<ITseDeviceHealthCheckService>(MockBehavior.Strict);
        var svc = CreateService(db, health.Object);

        var result = await svc.DiagnoseAndHealAsync(deviceId, "admin");

        Assert.False(result.Applied);
        Assert.Equal(TseHealingAttemptStatuses.DiagnosedOnly, result.Status);
        Assert.Equal(TseHealingConditions.DeviceOffline, result.MatchedCondition);
        Assert.Equal(1, await db.TseHealingHistory.CountAsync());
        health.Verify(h => h.CheckHealthAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _ = tenantId;
    }

    [Fact]
    public async Task DiagnoseAndHealAsync_WhenEnabled_ReprobesOfflineDevice()
    {
        await using var db = CreateDb();
        var (_, deviceId) = await SeedOfflineDeviceAsync(db);
        var health = new Mock<ITseDeviceHealthCheckService>();
        health.Setup(h => h.CheckHealthAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseHealthResult
            {
                DeviceId = deviceId,
                IsHealthy = true,
                HealthScore = 90,
                Status = TseHealthStatus.Healthy,
                Message = "ok",
            });

        var svc = CreateService(db, health.Object);
        await svc.ConfigureHealingAsync(
            (await db.TseDevices.AsNoTracking().FirstAsync(d => d.Id == deviceId)).TenantId!.Value,
            new ConfigureTseHealingRequestDto
            {
                Enabled = true,
                MaxAutoHealAttempts = 3,
                CooldownMinutes = 30,
                NotifyOnHeal = false,
                AllowAutoFailover = false,
            },
            "admin");

        var result = await svc.DiagnoseAndHealAsync(deviceId, "admin");

        Assert.True(result.Applied);
        Assert.Equal(TseHealingAttemptStatuses.Succeeded, result.Status);
        Assert.Equal(TseHealingActions.ReprobeHealth, result.ActionTaken);
        Assert.Equal(90, result.HealthScoreAfter);
        health.Verify(h => h.CheckHealthAsync(deviceId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DiagnoseAndHealAsync_FailoverBlockedWhenNotAllowed()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId) = await SeedUnhealthyPrimaryAsync(db);
        var failover = new Mock<ITseFailoverService>(MockBehavior.Strict);
        var svc = CreateService(db, failover: failover.Object);

        await svc.ConfigureHealingAsync(
            tenantId,
            new ConfigureTseHealingRequestDto
            {
                Enabled = true,
                MaxAutoHealAttempts = 3,
                CooldownMinutes = 30,
                NotifyOnHeal = false,
                AllowAutoFailover = false,
                Rules =
                [
                    new ConfigureTseHealingRuleDto
                    {
                        Condition = TseHealingConditions.PrimaryUnhealthyWithBackup,
                        Action = TseHealingActions.AttemptFailover,
                        Priority = 1,
                        Status = TseHealingRuleStatuses.Enabled,
                    },
                ],
            },
            "admin");

        var result = await svc.DiagnoseAndHealAsync(deviceId, "admin");

        Assert.False(result.Applied);
        Assert.Equal(TseHealingAttemptStatuses.DiagnosedOnly, result.Status);
        Assert.Contains("AllowAutoFailover", result.Message, StringComparison.Ordinal);
        failover.Verify(
            f => f.CheckAndFailoverAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetHealingHistoryAsync_ReturnsNewestFirst()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId) = await SeedOfflineDeviceAsync(db);
        db.TseHealingHistory.AddRange(
            new TseHealingHistoryEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DeviceId = deviceId,
                Status = TseHealingAttemptStatuses.DiagnosedOnly,
                Message = "old",
                StartedAt = DateTime.UtcNow.AddHours(-2),
                CompletedAt = DateTime.UtcNow.AddHours(-2),
            },
            new TseHealingHistoryEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DeviceId = deviceId,
                Status = TseHealingAttemptStatuses.Succeeded,
                Applied = true,
                Message = "new",
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                CompletedAt = DateTime.UtcNow.AddMinutes(-5),
            });
        await db.SaveChangesAsync();

        var report = await CreateService(db).GetHealingHistoryAsync(tenantId);
        Assert.Equal(2, report.Items.Count);
        Assert.Equal("new", report.Items[0].Message);
        Assert.Equal(1, report.SucceededCount);
        Assert.Equal(1, report.AppliedCount);
    }

    private static TseAutoHealingService CreateService(
        AppDbContext db,
        ITseDeviceHealthCheckService? health = null,
        ITseFailoverService? failover = null)
    {
        var monitor = new Mock<IOptionsMonitor<TseOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(new TseOptions
        {
            FailoverHealthyMinScore = 80,
            FailoverDegradedMinScore = 50,
        });

        return new TseAutoHealingService(
            db,
            health ?? Mock.Of<ITseDeviceHealthCheckService>(),
            failover ?? Mock.Of<ITseFailoverService>(),
            monitor.Object,
            Mock.Of<IActivityEventPublisher>(),
            NullLogger<TseAutoHealingService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_autoheal_{Guid.NewGuid():N}")
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
            Name = "Heal Cafe",
            Slug = "heal-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static async Task<(Guid TenantId, Guid DeviceId)> SeedOfflineDeviceAsync(AppDbContext db)
    {
        var tenantId = await SeedTenantAsync(db);
        var deviceId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.TseDevices.Add(CreateDevice(tenantId, deviceId, now, connected: false, score: 0, TseHealthStatus.Offline));
        await db.SaveChangesAsync();
        return (tenantId, deviceId);
    }

    private static async Task<(Guid TenantId, Guid DeviceId)> SeedUnhealthyPrimaryAsync(AppDbContext db)
    {
        var tenantId = await SeedTenantAsync(db);
        var deviceId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.TseDevices.Add(CreateDevice(tenantId, deviceId, now, connected: true, score: 20, TseHealthStatus.Degraded));
        await db.SaveChangesAsync();
        return (tenantId, deviceId);
    }

    private static TseDevice CreateDevice(
        Guid tenantId,
        Guid deviceId,
        DateTime now,
        bool connected,
        int score,
        TseHealthStatus status) =>
        new()
        {
            Id = deviceId,
            TenantId = tenantId,
            SerialNumber = "HEAL-1",
            DeviceType = "fiskaly",
            Provider = "fiskaly",
            VendorId = "v",
            ProductId = "p",
            IsConnected = connected,
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
            HealthStatus = status,
            HealthScore = score,
            IsPrimary = true,
            LastHealthCheck = now,
        };
}
