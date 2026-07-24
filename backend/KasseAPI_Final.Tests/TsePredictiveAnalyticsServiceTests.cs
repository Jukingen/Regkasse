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

public sealed class TsePredictiveAnalyticsServiceTests
{
    [Fact]
    public async Task PredictFailureAsync_FlagsCriticalWhenOfflineAndCertExpired()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId) = await SeedDeviceAsync(db, score: 20, status: TseHealthStatus.Offline);
        var device = await db.TseDevices.FirstAsync(d => d.Id == deviceId);
        device.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        device.IsConnected = false;
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        db.TseDeviceHealthSamples.AddRange(
            Sample(deviceId, tenantId, now.AddDays(-3), 80, TseHealthStatus.Healthy, 200),
            Sample(deviceId, tenantId, now.AddDays(-2), 50, TseHealthStatus.Degraded, 4000),
            Sample(deviceId, tenantId, now.AddDays(-1), 20, TseHealthStatus.Offline, 12000));
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

        var svc = CreateService(db, activity.Object);
        var prediction = await svc.PredictFailureAsync(deviceId);

        Assert.True(prediction.FailureProbability >= 55);
        Assert.True(
            prediction.RiskLevel is TsePredictiveRiskLevels.High or TsePredictiveRiskLevels.Critical);
        Assert.True(prediction.RequiresImmediateAction);
        Assert.Contains(prediction.RiskFactors, f => f.Code == "certificate_expired");
        Assert.True(prediction.AlertPublished);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.TsePredictiveFailureRisk,
                It.IsAny<object?>(),
                "system",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ForecastHealthAsync_ProjectsDecliningScores()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId) = await SeedDeviceAsync(db, score: 90, status: TseHealthStatus.Healthy);
        var now = DateTime.UtcNow;

        db.TseDeviceHealthSamples.AddRange(
            Sample(deviceId, tenantId, now.AddDays(-6), 100, TseHealthStatus.Healthy, 100),
            Sample(deviceId, tenantId, now.AddDays(-4), 90, TseHealthStatus.Healthy, 120),
            Sample(deviceId, tenantId, now.AddDays(-2), 80, TseHealthStatus.Degraded, 200),
            Sample(deviceId, tenantId, now.AddHours(-6), 70, TseHealthStatus.Degraded, 300));
        await db.SaveChangesAsync();

        // Align live score with latest sample for clearer forecast.
        var device = await db.TseDevices.FirstAsync(d => d.Id == deviceId);
        device.HealthScore = 70;
        device.HealthStatus = TseHealthStatus.Degraded;
        await db.SaveChangesAsync();

        var svc = CreateService(db, Mock.Of<IActivityEventPublisher>());
        var forecast = await svc.ForecastHealthAsync(deviceId, days: 7);

        Assert.Equal(7, forecast.ForecastPoints.Count);
        Assert.True(forecast.HealthTrendPerDay < 0);
        Assert.True(forecast.PredictedHealthScoreAtHorizon <= forecast.CurrentHealthScore);
    }

    [Fact]
    public async Task IdentifyRiskFactorsAsync_IncludesMissingBackup()
    {
        await using var db = CreateDb();
        var (tenantId, _) = await SeedDeviceAsync(db, score: 95, status: TseHealthStatus.Healthy);

        var svc = CreateService(db, Mock.Of<IActivityEventPublisher>());
        var factors = await svc.IdentifyRiskFactorsAsync(tenantId);

        Assert.Contains(factors, f => f.Code == "no_healthy_backup");
    }

    private static TsePredictiveAnalyticsService CreateService(
        AppDbContext db,
        IActivityEventPublisher activity,
        TseOptions? opts = null)
    {
        return new TsePredictiveAnalyticsService(
            db,
            Options.Create(opts ?? new TseOptions
            {
                FailoverHealthyMinScore = 80,
                FailoverDegradedMinScore = 50,
                HealthSlowResponseMs = 3000,
                HealthCriticalResponseMs = 10000,
                HealthErrorRateWarningPercent = 20,
                CertificateExpiringSoonDays = 30,
                PredictiveLookbackDays = 14,
                PredictiveDeclinePerDayWarning = 1.5,
                PredictiveFailoverCountWarning = 2,
                PredictiveMediumProbability = 30,
                PredictiveHighProbability = 55,
                PredictiveCriticalProbability = 75,
            }).ToMonitor(),
            activity,
            NullLogger<TsePredictiveAnalyticsService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_predict_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<(Guid TenantId, Guid DeviceId)> SeedDeviceAsync(
        AppDbContext db,
        int score,
        TseHealthStatus status)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Predict Cafe",
            Slug = "predict-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-PR1",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        var device = new TseDevice
        {
            SerialNumber = "PRED-1",
            DeviceType = "Cloud",
            VendorId = "auto",
            ProductId = "fiskaly",
            Provider = TseOptions.ProviderFiskaly,
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "predict-device",
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = true,
            HealthStatus = status,
            HealthScore = score,
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
