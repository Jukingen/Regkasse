using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseMetricsServiceTests
{
    [Fact]
    public async Task GetSummaryMetricsAsync_CountsByStatusAndFailover()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "M",
            Slug = "m",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.TseDevices.AddRange(
            Device(tenantId, "A", TseHealthStatus.Healthy, 100, isPrimary: true),
            Device(tenantId, "B", TseHealthStatus.Degraded, 70, isPrimary: true),
            Device(tenantId, "C", TseHealthStatus.Offline, 0, isBackup: true, failover: true));
        await db.SaveChangesAsync();

        var svc = new TseMetricsService(db);
        var summary = await svc.GetSummaryMetricsAsync();

        Assert.Equal(3, summary.ActiveDevices);
        Assert.Equal(1, summary.HealthyDevices);
        Assert.Equal(1, summary.DegradedDevices);
        Assert.Equal(1, summary.UnhealthyDevices);
        Assert.Equal(1, summary.OfflineDevices);
        Assert.Equal(1, summary.ActiveFailoverCount);
        Assert.Equal(2, summary.PrimaryDevices);
        Assert.Equal(1, summary.BackupDevices);
        Assert.Equal(Math.Round((100 + 70 + 0) / 3.0, 3), summary.AverageHealthScore);
        Assert.Equal(0.5, TseMetricsService.ResolveFleetGauge(summary));
    }

    [Fact]
    public async Task GetPrometheusMetricsAsync_EmitsExpectedSeries()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "P",
            Slug = "p",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var device = Device(tenantId, "SN-1", TseHealthStatus.Healthy, 95, isPrimary: true);
        device.Provider = "fiskaly";
        db.TseDevices.Add(device);
        await db.SaveChangesAsync();

        var text = await new TseMetricsService(db).GetPrometheusMetricsAsync();

        Assert.Contains("# TYPE tse_devices_total gauge", text);
        Assert.Contains("tse_devices_total{status=\"healthy\"} 1", text);
        Assert.Contains("tse_devices_by_provider{provider=\"fiskaly\"} 1", text);
        Assert.Contains($"tse_device_health_score{{device_id=\"{device.Id:D}\"", text);
        Assert.Contains("tse_fleet_status 1", text);
        Assert.Contains("tse_failover_active 0", text);
        Assert.DoesNotContain("\n\r", text);
    }

    [Fact]
    public void EscapeLabel_EscapesQuotesAndBackslashes()
    {
        Assert.Equal("a\\\"b\\\\c", TseMetricsService.EscapeLabel("a\"b\\c"));
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_metrics_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TseDevice Device(
        Guid tenantId,
        string serial,
        TseHealthStatus status,
        int score,
        bool isPrimary = false,
        bool isBackup = false,
        bool failover = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SerialNumber = serial,
            DeviceType = "fiskaly",
            Provider = "fiskaly",
            VendorId = "v",
            ProductId = "p",
            IsConnected = status == TseHealthStatus.Healthy,
            LastConnectionTime = DateTime.UtcNow,
            LastSignatureTime = DateTime.UtcNow,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            CanCreateInvoices = true,
            FinanzOnlineUsername = "fo",
            FinanzOnlineEnabled = false,
            LastFinanzOnlineSync = DateTime.UtcNow,
            KassenId = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            HealthStatus = status,
            HealthScore = score,
            LastHealthCheck = DateTime.UtcNow.AddMinutes(-5),
            IsPrimary = isPrimary,
            IsBackup = isBackup,
            IsFailoverActive = failover,
        };
}

public sealed class TseFleetHealthCheckServiceTests
{
    [Fact]
    public async Task GetOverallStatusAsync_Cached_ReturnsDegradedWhenMixed()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "H",
            Slug = "h",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.TseDevices.AddRange(
            Device(tenantId, "ok", TseHealthStatus.Healthy, 100),
            Device(tenantId, "bad", TseHealthStatus.Offline, 0));
        await db.SaveChangesAsync();

        var deviceHealth = new Mock<ITseDeviceHealthCheckService>();
        var metrics = new TseMetricsService(db);
        var svc = new TseFleetHealthCheckService(db, deviceHealth.Object, metrics);

        var status = await svc.GetOverallStatusAsync(liveProbe: false);

        Assert.Equal("degraded", status.Status);
        Assert.False(status.LiveProbe);
        Assert.Equal(2, status.DeviceCount);
        Assert.Equal(1, status.HealthyCount);
        deviceHealth.Verify(
            x => x.CheckAllDevicesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOverallStatusAsync_LiveProbe_DelegatesToDeviceHealth()
    {
        await using var db = CreateDb();
        var id = Guid.NewGuid();
        db.TseDevices.Add(Device(Guid.NewGuid(), "live", TseHealthStatus.Healthy, 100, id));
        await db.SaveChangesAsync();

        var deviceHealth = new Mock<ITseDeviceHealthCheckService>();
        deviceHealth
            .Setup(x => x.CheckAllDevicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TseHealthResult>
            {
                new()
                {
                    DeviceId = id,
                    IsHealthy = true,
                    HealthScore = 99,
                    Status = TseHealthStatus.Healthy,
                    Message = "ok",
                    CheckedAt = DateTime.UtcNow,
                    ResponseTimeMs = 12,
                },
            });

        var svc = new TseFleetHealthCheckService(db, deviceHealth.Object, new TseMetricsService(db));
        var status = await svc.GetOverallStatusAsync(liveProbe: true);

        Assert.Equal("healthy", status.Status);
        Assert.True(status.LiveProbe);
        Assert.Single(status.Devices);
        Assert.Equal(99, status.Devices[0].HealthScore);
        Assert.Equal(12, status.Devices[0].ResponseTimeMs);
    }

    [Theory]
    [InlineData(true, false, "healthy")]
    [InlineData(true, true, "degraded")]
    [InlineData(false, false, "unhealthy")]
    public void ResolveOverallStatus_MapsExpected(bool includeHealthy, bool includeDegraded, string expected)
    {
        var devices = new List<TseFleetDeviceHealthDto>();
        if (includeHealthy)
        {
            devices.Add(new TseFleetDeviceHealthDto
            {
                DeviceId = Guid.NewGuid(),
                IsHealthy = true,
                Status = nameof(TseHealthStatus.Healthy),
            });
        }

        if (includeDegraded)
        {
            devices.Add(new TseFleetDeviceHealthDto
            {
                DeviceId = Guid.NewGuid(),
                IsHealthy = false,
                Status = nameof(TseHealthStatus.Degraded),
            });
        }

        if (!includeHealthy && !includeDegraded)
        {
            devices.Add(new TseFleetDeviceHealthDto
            {
                DeviceId = Guid.NewGuid(),
                IsHealthy = false,
                Status = nameof(TseHealthStatus.Offline),
            });
        }

        Assert.Equal(expected, TseFleetHealthCheckService.ResolveOverallStatus(devices));
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_fleet_health_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TseDevice Device(
        Guid tenantId,
        string serial,
        TseHealthStatus status,
        int score,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            SerialNumber = serial,
            DeviceType = "fiskaly",
            Provider = "fiskaly",
            VendorId = "v",
            ProductId = "p",
            IsConnected = true,
            LastConnectionTime = DateTime.UtcNow,
            LastSignatureTime = DateTime.UtcNow,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            CanCreateInvoices = true,
            FinanzOnlineUsername = "fo",
            FinanzOnlineEnabled = false,
            LastFinanzOnlineSync = DateTime.UtcNow,
            KassenId = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            HealthStatus = status,
            HealthScore = score,
            LastHealthCheck = DateTime.UtcNow,
            IsPrimary = true,
        };
}

public sealed class AdminTseHealthControllerTests
{
    [Fact]
    public async Task GetOverallStatus_ReturnsOkDto()
    {
        var health = new Mock<ITseHealthCheckService>();
        health
            .Setup(x => x.GetOverallStatusAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseFleetHealthStatusDto
            {
                Status = "healthy",
                DeviceCount = 1,
                Devices =
                [
                    new TseFleetDeviceHealthDto
                    {
                        DeviceId = Guid.NewGuid(),
                        Status = "Healthy",
                        HealthScore = 100,
                        IsHealthy = true,
                        Message = "ok",
                    },
                ],
            });

        var controller = new AdminTseHealthController(health.Object, Mock.Of<ITseMetricsService>());
        var result = await controller.GetOverallStatus(liveProbe: true);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TseFleetHealthStatusDto>(ok.Value);
        Assert.Equal("healthy", dto.Status);
    }

    [Fact]
    public async Task GetPrometheusMetrics_ReturnsPlainText()
    {
        var metrics = new Mock<ITseMetricsService>();
        metrics
            .Setup(x => x.GetPrometheusMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("# HELP tse_fleet_status\ntse_fleet_status 1\n");

        var controller = new AdminTseHealthController(Mock.Of<ITseHealthCheckService>(), metrics.Object);
        var result = await controller.GetPrometheusMetrics();

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(AdminTseHealthController.PrometheusContentType, content.ContentType);
        Assert.Contains("tse_fleet_status 1", content.Content);
    }
}
