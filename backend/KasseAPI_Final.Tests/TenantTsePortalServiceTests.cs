using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantTsePortalServiceTests
{
    [Fact]
    public async Task GetStatusAsync_MapsDevicesAndOverallHealthy()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithHealthyDeviceAsync(db);

        var status = await new TenantTsePortalService(db).GetStatusAsync(tenantId);

        Assert.Equal(tenantId, status.TenantId);
        Assert.Equal("Healthy", status.OverallHealth);
        Assert.Single(status.Devices);
        Assert.True(status.Devices[0].IsPrimary);
        Assert.Equal(100, status.OverallHealthScore);
        Assert.Equal(40, status.NearestDaysUntilExpiry);
    }

    [Fact]
    public async Task GetStatusAsync_WhenAnyUnhealthy_IsDegraded()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithHealthyDeviceAsync(db);
        var now = DateTime.UtcNow;
        db.TseDevices.Add(new TseDevice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SerialNumber = "BACKUP-1",
            DeviceType = "fiskaly",
            Provider = "fiskaly",
            VendorId = "v",
            ProductId = "p",
            IsConnected = false,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            CanCreateInvoices = false,
            FinanzOnlineUsername = "fo",
            FinanzOnlineEnabled = false,
            LastFinanzOnlineSync = now,
            KassenId = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = now,
            HealthStatus = TseHealthStatus.Unhealthy,
            HealthScore = 40,
            IsPrimary = false,
            IsBackup = true,
            ExpiresAt = now.AddDays(10),
            LastHealthCheck = now,
        });
        await db.SaveChangesAsync();

        var status = await new TenantTsePortalService(db).GetStatusAsync(tenantId);

        Assert.Equal("Degraded", status.OverallHealth);
        Assert.Equal(2, status.Devices.Count);
        Assert.Equal(10, status.NearestDaysUntilExpiry);
    }

    [Fact]
    public async Task GetHealthHistoryAsync_ReturnsSamplesForTenant()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithHealthyDeviceAsync(db);
        var deviceId = await db.TseDevices.Where(d => d.TenantId == tenantId).Select(d => d.Id).FirstAsync();
        var now = DateTime.UtcNow;
        db.TseDeviceHealthSamples.AddRange(
            new TseDeviceHealthSample
            {
                DeviceId = deviceId,
                TenantId = tenantId,
                CheckedAtUtc = now.AddDays(-2),
                HealthScore = 90,
                HealthStatus = TseHealthStatus.Healthy,
                ResponseTimeMs = 100,
            },
            new TseDeviceHealthSample
            {
                DeviceId = deviceId,
                TenantId = tenantId,
                CheckedAtUtc = now.AddDays(-1),
                HealthScore = 85,
                HealthStatus = TseHealthStatus.Degraded,
                ResponseTimeMs = 200,
            });
        await db.SaveChangesAsync();

        var history = await new TenantTsePortalService(db).GetHealthHistoryAsync(tenantId, days: 30);

        Assert.Equal(2, history.Points.Count);
        Assert.Equal(30, history.Days);
    }

    [Fact]
    public async Task GetStatusAsync_UnknownTenant_Throws()
    {
        await using var db = CreateDb();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new TenantTsePortalService(db).GetStatusAsync(Guid.NewGuid()));
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tenant_tse_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<Guid> SeedTenantWithHealthyDeviceAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Portal Cafe",
            Slug = "portal-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = now,
        });
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
            ExpiresAt = now.AddDays(40),
            LastHealthCheck = now,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }
}
