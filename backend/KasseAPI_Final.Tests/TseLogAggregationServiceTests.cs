using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseLogAggregationServiceTests
{
    [Fact]
    public async Task AggregateLogsAsync_CountsLevelsAndSources()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId) = await SeedAsync(db);
        var now = DateTime.UtcNow;

        db.ActivityEvents.Add(new ActivityEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Type = ActivityEventType.TsePerformanceSlow,
            Severity = ActivitySeverityNames.Warning,
            Title = "Slow probe",
            Description = "avg 5000ms",
            EntityType = "TseDevice",
            EntityId = deviceId.ToString("D"),
            CreatedAtUtc = now.AddHours(-2),
        });
        db.TseFailoverLogs.Add(new TseFailoverLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PrimaryDeviceId = deviceId,
            FailoverType = TseFailoverTypes.Automatic,
            TriggerReason = TseFailoverTriggerReasons.HealthCheckFailed,
            IsSuccessful = false,
            ErrorMessage = "backup unhealthy",
            StartedAt = now.AddHours(-1),
        });
        db.TseDeviceHealthSamples.Add(new TseDeviceHealthSample
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            TenantId = tenantId,
            CheckedAtUtc = now.AddMinutes(-30),
            HealthScore = 20,
            HealthStatus = TseHealthStatus.Unhealthy,
            Message = "probe failed",
            IsPrimary = true,
            ResponseTimeMs = 8000,
        });
        await db.SaveChangesAsync();

        var svc = new TseLogAggregationService(db, NullLogger<TseLogAggregationService>.Instance);
        var result = await svc.AggregateLogsAsync(tenantId, now.AddDays(-1), now);

        Assert.True(result.TotalLogs >= 3);
        Assert.True(result.ErrorLogs >= 1);
        Assert.True(result.WarningLogs >= 1);
        Assert.Contains(TseLogSources.Activity, result.LogsBySource.Keys);
        Assert.Contains(TseLogSources.Failover, result.LogsBySource.Keys);
        Assert.Contains(TseLogSources.HealthSample, result.LogsBySource.Keys);
        Assert.NotEmpty(result.RecentLogs);
    }

    [Fact]
    public async Task SearchLogsAsync_FiltersByQueryAndLevel()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId) = await SeedAsync(db);
        var now = DateTime.UtcNow;

        db.TseFailoverLogs.Add(new TseFailoverLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PrimaryDeviceId = deviceId,
            FailoverType = TseFailoverTypes.Manual,
            TriggerReason = TseFailoverTriggerReasons.ManualOverride,
            IsSuccessful = true,
            Notes = "operator switch",
            StartedAt = now.AddHours(-3),
        });
        await db.SaveChangesAsync();

        var svc = new TseLogAggregationService(db, NullLogger<TseLogAggregationService>.Instance);
        var result = await svc.SearchLogsAsync(new TseLogSearchRequestDto
        {
            TenantId = tenantId,
            FromUtc = now.AddDays(-1),
            ToUtc = now,
            Query = "operator",
            Level = "Warning",
            Take = 50,
        });

        Assert.True(result.TotalMatched >= 1);
        Assert.All(result.Logs, l => Assert.Equal(TseLogLevels.Warning, l.Level));
        Assert.Contains(result.Logs, l => l.Message.Contains("operator", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeLogsAsync_ProducesSummaryAndRecommendations()
    {
        await using var db = CreateDb();
        var (tenantId, deviceId) = await SeedAsync(db);
        var now = DateTime.UtcNow;

        for (var i = 0; i < 6; i++)
        {
            db.TseFailoverLogs.Add(new TseFailoverLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PrimaryDeviceId = deviceId,
                FailoverType = TseFailoverTypes.Automatic,
                TriggerReason = TseFailoverTriggerReasons.HealthCheckFailed,
                IsSuccessful = false,
                ErrorMessage = "fail",
                StartedAt = now.AddMinutes(-(i + 1)),
            });
        }

        await db.SaveChangesAsync();
        var svc = new TseLogAggregationService(db, NullLogger<TseLogAggregationService>.Instance);
        var report = await svc.AnalyzeLogsAsync(tenantId, new TseLogAnalysisRequestDto
        {
            FromUtc = now.AddDays(-1),
            ToUtc = now,
        });

        Assert.False(string.IsNullOrWhiteSpace(report.Summary));
        Assert.True(report.ErrorRatePercent > 0);
        Assert.NotEmpty(report.Recommendations);
        Assert.Contains(report.Anomalies, a => a.Code is "high_error_rate" or "failover_failures" or "error_burst");
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_logs_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<(Guid TenantId, Guid DeviceId)> SeedAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Log Cafe",
            Slug = "log-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-LOG",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        var device = new TseDevice
        {
            SerialNumber = "LOG-1",
            DeviceType = "fiskaly",
            VendorId = "auto",
            ProductId = "soft",
            Provider = "fiskaly",
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "log-device",
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
}
