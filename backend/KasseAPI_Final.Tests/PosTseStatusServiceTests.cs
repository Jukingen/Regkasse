using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PosTseStatusServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"pos_tse_status_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static PosTseStatusService CreateService(
        AppDbContext db,
        TseHealthSnapshot snapshot,
        string environmentName = "Production",
        bool simulateUnavailable = false)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environmentName);
        var dev = Options.Create(new DevelopmentOptions { SimulateTseUnavailable = simulateUnavailable });
        return new PosTseStatusService(db, new FixedTseHealthMonitor(snapshot), env.Object, dev.ToMonitor());
    }

    [Fact]
    public async Task GetStatusAsync_OnlineConnectedDevice_ReturnsActive()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe-pos-tse",
            Status = TenantStatuses.Active,
            TseScuId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
            TseStatus = TenantTseStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Hauptkasse",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        db.TseDevices.Add(new TseDevice
        {
            TenantId = tenantId,
            KassenId = register.Id,
            SerialNumber = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
            DeviceType = "fiskaly",
            VendorId = "VID",
            ProductId = "PID",
            IsConnected = true,
            CanCreateInvoices = true,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddYears(2),
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db, new TseHealthSnapshot
        {
            Status = TseOperationalHealth.Online,
            LastCheckUtc = DateTime.UtcNow,
            LastSuccessfulPingUtc = DateTime.UtcNow,
        });

        var dto = await svc.GetStatusAsync(tenantId, register.Id);

        Assert.Equal(PosTseIndicatorStatuses.Active, dto.Status);
        Assert.Equal("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", dto.ScuId);
        Assert.Equal(dto.ScuId, dto.TssId);
        Assert.False(dto.Cached);
        Assert.Equal("Online", dto.OperationalHealth);
        Assert.NotNull(dto.CertificateValidUntil);
    }

    [Fact]
    public async Task GetStatusAsync_OfflineWithCachedPing_ReturnsDegraded()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cache-tse",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db, new TseHealthSnapshot
        {
            Status = TseOperationalHealth.Offline,
            LastCheckUtc = DateTime.UtcNow,
            LastSuccessfulPingUtc = DateTime.UtcNow.AddMinutes(-2),
            ConsecutiveFailures = 3,
            LastErrorMessageSafe = "timeout",
        });

        var dto = await svc.GetStatusAsync(tenantId, null);

        Assert.Equal(PosTseIndicatorStatuses.Degraded, dto.Status);
        Assert.True(dto.Cached);
        Assert.Contains("cached", dto.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStatusAsync_OfflineWithoutCache_ReturnsInactive()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "down-tse",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db, new TseHealthSnapshot
        {
            Status = TseOperationalHealth.Offline,
            LastCheckUtc = DateTime.UtcNow,
            ConsecutiveFailures = 5,
        });

        var dto = await svc.GetStatusAsync(tenantId, null);

        Assert.Equal(PosTseIndicatorStatuses.Inactive, dto.Status);
        Assert.False(dto.Cached);
        Assert.Equal("Offline", dto.OperationalHealth);
    }

    [Fact]
    public async Task GetStatusAsync_SoftFallback_ReturnsDegraded()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "soft-tse",
            Status = TenantStatuses.Active,
            TseStatus = TenantTseStatuses.SoftFallback,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Hauptkasse",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        db.TseDevices.Add(new TseDevice
        {
            TenantId = tenantId,
            KassenId = register.Id,
            SerialNumber = "AUTO-Soft",
            DeviceType = "Soft",
            VendorId = "VID",
            ProductId = "PID",
            IsConnected = true,
            CanCreateInvoices = true,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db, new TseHealthSnapshot
        {
            Status = TseOperationalHealth.Online,
            LastCheckUtc = DateTime.UtcNow,
            LastSuccessfulPingUtc = DateTime.UtcNow,
        });

        var dto = await svc.GetStatusAsync(tenantId, null);

        Assert.Equal(PosTseIndicatorStatuses.Degraded, dto.Status);
        Assert.Contains("Soft TSE", dto.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStatusAsync_DevSimulateUnavailable_ReturnsInactive()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var svc = CreateService(
            db,
            new TseHealthSnapshot { Status = TseOperationalHealth.Online },
            environmentName: Environments.Development,
            simulateUnavailable: true);

        var dto = await svc.GetStatusAsync(tenantId, null);

        Assert.Equal(PosTseIndicatorStatuses.Inactive, dto.Status);
        Assert.Equal("Offline", dto.OperationalHealth);
    }
}

file sealed class FixedTseHealthMonitor(TseHealthSnapshot snapshot) : ITseHealthMonitor
{
    public TseHealthSnapshot Snapshot { get; } = snapshot;

    public event EventHandler<TseHealthChangedEventArgs>? StatusChanged
    {
        add { }
        remove { }
    }
}
