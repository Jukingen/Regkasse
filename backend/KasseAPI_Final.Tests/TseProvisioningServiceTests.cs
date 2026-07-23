using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using KasseAPI_Final.Tse.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseProvisioningServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_provision_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TseProvisioningService CreateService(
        AppDbContext db,
        TseOptions? tseOptions = null,
        FiskalyOptions? fiskalyOptions = null,
        bool providerReady = true)
    {
        var tse = tseOptions ?? new TseOptions
        {
            TseMode = "Demo",
            Mode = "Fake",
            AutoProvisionOnTenantCreate = true,
        };
        var fiskaly = fiskalyOptions ?? new FiskalyOptions { Enabled = false };

        var provider = new Mock<ITseProvider>();
        provider.Setup(p => p.IsReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(providerReady);

        var tseMonitor = Options.Create(tse).ToMonitor();
        var fiskalyMonitor = Options.Create(fiskaly).ToMonitor();
        var keyProvider = Mock.Of<ITseKeyProvider>();
        var factory = new TseProviderFactory(
            new FakeTseProvider(NullLogger<FakeTseProvider>.Instance),
            new RealTseProvider(
                new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance),
                keyProvider,
                db,
                NullLogger<RealTseProvider>.Instance),
            tseMonitor,
            fiskalyMonitor,
            NullLogger<TseProviderFactory>.Instance);

        return new TseProvisioningService(
            db,
            tseMonitor,
            fiskalyMonitor,
            provider.Object,
            factory,
            AlwaysOnlineTseHealthMonitor.Instance,
            Mock.Of<IAuditLogService>(),
            NullLogger<TseProvisioningService>.Instance);
    }

    [Fact]
    public async Task ProvisionTseForCashRegisterAsync_DemoMode_CreatesDeviceAndChain()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe-tse",
            Status = TenantStatuses.Active,
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
            IsDefaultForTenant = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.ProvisionTseForCashRegisterAsync(register.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(TseProvisioningOutcome.Success, result.Outcome);
        Assert.NotNull(result.Device);
        Assert.Equal(register.Id, result.Device!.KassenId);
        Assert.True(result.Device.IsConnected);
        Assert.True(result.Device.CanCreateInvoices);
        Assert.True(result.SignatureChainInitialized);
        Assert.False(result.StartbelegCreated);

        Assert.Equal(1, await db.TseDevices.CountAsync(d => d.KassenId == register.Id));
        Assert.Equal(1, await db.SignatureChainState.IgnoreQueryFilters()
            .CountAsync(s => s.CashRegisterId == register.Id && s.TenantId == tenantId));
    }

    [Fact]
    public async Task ProvisionTseForCashRegisterAsync_IsIdempotent()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe-idem",
            Status = TenantStatuses.Active,
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
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var first = await svc.ProvisionTseForCashRegisterAsync(register.Id);
        var second = await svc.ProvisionTseForCashRegisterAsync(register.Id);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Device!.Id, second.Device!.Id);
        Assert.Equal(1, await db.TseDevices.CountAsync(d => d.KassenId == register.Id));
    }

    [Fact]
    public async Task ProvisionTseForTenantAsync_OffMode_Skips()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Off",
            Slug = "off-tse",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db, new TseOptions { TseMode = "Off", Mode = "Fake" });
        var result = await svc.ProvisionTseForTenantAsync(tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(TseProvisioningOutcome.Skipped, result.Outcome);
        Assert.Equal(0, await db.TseDevices.CountAsync());
    }

    [Fact]
    public async Task ProvisionTseForTenantAsync_UsesDefaultCashRegister()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Multi",
            Slug = "multi-tse",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var secondary = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-002",
            Location = "Nebenkasse",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            IsActive = true,
            IsDefaultForTenant = false,
        };
        var primary = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Hauptkasse",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsDefaultForTenant = true,
        };
        db.CashRegisters.AddRange(secondary, primary);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.ProvisionTseForTenantAsync(tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(primary.Id, result.Device!.KassenId);
    }

    [Fact]
    public async Task GetTseStatusAsync_ReportsOperational_AfterProvision()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Status",
            Slug = "status-tse",
            Status = TenantStatuses.Active,
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
            IsDefaultForTenant = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.ProvisionTseForCashRegisterAsync(register.Id);
        var status = await svc.GetTseStatusAsync(tenantId);
        var health = await svc.PerformHealthCheckAsync(tenantId);

        Assert.Equal(1, status.DeviceCount);
        Assert.True(status.IsOperational);
        Assert.Equal("Operational", status.Status);
        Assert.True(health.IsHealthy);
    }

    [Fact]
    public async Task ListDevicesAsync_IncludesTenantViaCashRegister()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Fleet Cafe",
            Slug = "fleet-cafe",
            Status = TenantStatuses.Active,
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
            IsDefaultForTenant = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.ProvisionTseForCashRegisterAsync(register.Id);

        var list = await svc.ListDevicesAsync();
        Assert.Single(list);
        Assert.Equal(tenantId, list[0].TenantId);
        Assert.Equal("Fleet Cafe", list[0].TenantName);
        Assert.Equal("Active", list[0].Status);
        Assert.Equal(100, list[0].HealthScore);
    }

    [Fact]
    public async Task RevokeTseDeviceAsync_DeactivatesDevice()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Revoke Cafe",
            Slug = "revoke-cafe",
            Status = TenantStatuses.Active,
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
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var provisioned = await svc.ProvisionTseForCashRegisterAsync(register.Id);
        Assert.NotNull(provisioned.Device);

        var revoked = await svc.RevokeTseDeviceAsync(provisioned.Device!.Id, "super-admin");
        Assert.True(revoked.IsSuccess);
        Assert.False(revoked.Device!.IsActive);
        Assert.False(revoked.Device.IsConnected);
        Assert.False(revoked.Device.CanCreateInvoices);

        var overview = await svc.GetFleetOverviewAsync();
        Assert.Equal(1, overview.TotalDevices);
        Assert.Equal(1, overview.InactiveDevices);
        Assert.Equal(0, overview.ActiveDevices);
    }

    [Fact]
    public async Task ProvisionTseForTenantAsync_Force_IgnoresAutoProvisionFlag()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Force",
            Slug = "force-tse",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Hauptkasse",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsDefaultForTenant = true,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db, new TseOptions
        {
            TseMode = "Demo",
            Mode = "Fake",
            AutoProvisionOnTenantCreate = false,
        });

        var skipped = await svc.ProvisionTseForTenantAsync(tenantId, force: false);
        Assert.Equal(TseProvisioningOutcome.Skipped, skipped.Outcome);

        var forced = await svc.ProvisionTseForTenantAsync(tenantId, force: true);
        Assert.Equal(TseProvisioningOutcome.Success, forced.Outcome);
        Assert.NotNull(forced.Device);
    }

    [Fact]
    public async Task ProvisionTseForCashRegisterAsync_DeviceModeWithoutFiskaly_CreatesPendingDevice()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Device",
            Slug = "device-tse",
            Status = TenantStatuses.Active,
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
        await db.SaveChangesAsync();

        var svc = CreateService(db, new TseOptions { TseMode = "Device", Mode = "Real" });
        var result = await svc.ProvisionTseForCashRegisterAsync(register.Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Device);
        Assert.False(result.Device!.IsConnected);
        Assert.False(result.Device.CanCreateInvoices);
        Assert.Equal("Device", result.Device.DeviceType);
    }
}

internal static class OptionsMonitorExtensions
{
    public static IOptionsMonitor<T> ToMonitor<T>(this IOptions<T> options) where T : class, new()
    {
        var mock = new Mock<IOptionsMonitor<T>>();
        mock.Setup(m => m.CurrentValue).Returns(options.Value);
        mock.Setup(m => m.Get(It.IsAny<string?>())).Returns(options.Value);
        return mock.Object;
    }
}
