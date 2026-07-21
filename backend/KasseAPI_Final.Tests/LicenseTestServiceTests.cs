using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.LicenseTest;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseTestServiceTests
{
    private static AppDbContext CreateDb()
    {
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"LicenseTest_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            NullCurrentTenantAccessor.Instance);
    }

    private sealed class InMemoryLicenseStorage : ILicenseStorageService
    {
        private LicensePersistedState? _state;

        public string LicenseFilePath => "memory://license.dat";
        public string MachineHashHex { get; } = "abc123machinehash0000000000000000000000000000000000000000000000";
        public string MachineFingerprintCanonical => "test-machine";

        public LicensePersistedState? LoadLicenseFromFile() => _state;

        public void SaveLicenseToFile(LicensePersistedState state) => _state = state;
    }

    private static LicenseTestService CreateService(
        AppDbContext db,
        InMemoryLicenseStorage storage,
        bool isDevelopment = true)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName)
            .Returns(isDevelopment ? Environments.Development : Environments.Production);

        var licenseService = new Mock<ILicenseService>();
        licenseService.Setup(s => s.GetLicenseStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseStatusInfo
            {
                IsActive = true,
                DaysRemaining = 10,
                CanAccess = true,
                CanTransact = true,
                StatusMessage = "ok",
            });
        licenseService.Setup(s => s.GetCurrentDeploymentStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseStatusResponse(
                true,
                false,
                false,
                10,
                DateTime.UtcNow.AddDays(10),
                storage.MachineHashHex));

        var devMode = new Mock<IDevelopmentModeService>();
        devMode.Setup(d => d.ShouldBypassLicense()).Returns(false);

        return new LicenseTestService(
            db,
            licenseService.Object,
            storage,
            devMode.Object,
            env.Object,
            NullLogger<LicenseTestService>.Instance);
    }

    [Fact]
    public async Task SetTenantExpiryAsync_UpdatesTenantRow()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Dev Cafe",
            Slug = "dev-cafe",
            Status = TenantStatuses.Active,
            LicenseValidUntilUtc = DateTime.UtcNow.AddDays(100),
        });
        await db.SaveChangesAsync();

        var storage = new InMemoryLicenseStorage();
        var svc = CreateService(db, storage);
        var target = DateTime.UtcNow.AddDays(7);

        var snapshot = await svc.SetTenantExpiryAsync(
            new LicenseTestTenantRequest { TenantId = tenantId, ValidUntilUtc = target });

        Assert.NotNull(snapshot.Tenant);
        Assert.Equal("dev-cafe", snapshot.Tenant!.Slug);
        Assert.Equal(target.Date, snapshot.Tenant.ValidUntilUtc!.Value.Date);
        Assert.InRange(snapshot.Tenant.DaysRemaining, 6, 7);

        var row = await db.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == tenantId);
        Assert.Equal(target.Date, row.LicenseValidUntilUtc!.Value.Date);
    }

    [Fact]
    public async Task SetTenantExpiryAsync_OutsideDevelopment_Throws()
    {
        await using var db = CreateDb();
        var storage = new InMemoryLicenseStorage();
        var svc = CreateService(db, storage, isDevelopment: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SetTenantExpiryAsync(new LicenseTestTenantRequest
            {
                TenantId = Guid.NewGuid(),
                SetExpired = true,
            }));
    }

    [Fact]
    public async Task UpdateAsync_SetsValidUntilAndTestLicenseKey()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "QA Tenant",
            Slug = "qa",
            Status = TenantStatuses.Active,
            LicenseValidUntilUtc = DateTime.UtcNow.AddDays(100),
            LicenseKey = "REGK-OLD-KEY",
        });
        await db.SaveChangesAsync();

        var storage = new InMemoryLicenseStorage();
        var svc = CreateService(db, storage);
        var target = DateTime.UtcNow.AddDays(14);

        var result = await svc.UpdateAsync(new LicenseTestRequest
        {
            TenantId = tenantId,
            ValidUntil = target,
        });

        Assert.NotNull(result.Tenant);
        Assert.Equal(target.Date, result.Tenant!.ValidUntilUtc!.Value.Date);

        var row = await db.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == tenantId);
        Assert.Equal(target.Date, row.LicenseValidUntilUtc!.Value.Date);
        Assert.StartsWith("TEST-", row.LicenseKey);
        Assert.Null(row.CurrentLicenseSaleId);
    }

    [Fact]
    public async Task UpdateAsync_FromUnlimitedNullExpiry_OverridesToOneDay()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Unlimited Dev",
            Slug = "dev",
            Status = TenantStatuses.Active,
            LicenseValidUntilUtc = null,
            LicenseKey = "REGK-OLD-KEY",
            CurrentLicenseSaleId = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        var storage = new InMemoryLicenseStorage();
        var svc = CreateService(db, storage);
        var target = DateTime.UtcNow.AddDays(1);

        var snapshot = await svc.UpdateAsync(new LicenseTestRequest
        {
            TenantId = tenantId,
            ValidUntil = target,
        });

        Assert.NotNull(snapshot.Tenant);
        Assert.Equal(1, snapshot.Tenant!.DaysRemaining);
        Assert.NotEqual(999, snapshot.Tenant.DaysRemaining);

        var row = await db.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == tenantId);
        Assert.Equal(target.Date, row.LicenseValidUntilUtc!.Value.Date);
        Assert.Null(row.CurrentLicenseSaleId);
    }

    [Fact]
    public async Task UpdateAsync_WithoutValidUntil_Throws()
    {
        await using var db = CreateDb();
        var storage = new InMemoryLicenseStorage();
        var svc = CreateService(db, storage);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateAsync(new LicenseTestRequest { TenantId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task GetSnapshotAsync_ReportsActualTenantDays_NotEnforcementOverlay()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var validUntil = DateTime.UtcNow.AddDays(1);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Dev",
            Slug = "dev",
            Status = TenantStatuses.Active,
            LicenseValidUntilUtc = validUntil,
            LicenseKey = "TEST-abc",
        });
        await db.SaveChangesAsync();

        var storage = new InMemoryLicenseStorage();
        var licenseService = new Mock<ILicenseService>();
        licenseService.Setup(s => s.GetLicenseStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LicenseService.CreateUnlimitedMandantLicenseStatus("Development Mode - Unlimited Access"));
        licenseService.Setup(s => s.GetCurrentDeploymentStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseStatusResponse(
                true,
                false,
                false,
                999,
                DateTime.UtcNow.AddDays(999),
                storage.MachineHashHex));

        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var devMode = new Mock<IDevelopmentModeService>();
        devMode.Setup(d => d.ShouldBypassLicense()).Returns(false);

        var svc = new LicenseTestService(
            db,
            licenseService.Object,
            storage,
            devMode.Object,
            env.Object,
            NullLogger<LicenseTestService>.Instance);

        var snapshot = await svc.GetSnapshotAsync(tenantId);

        Assert.NotNull(snapshot.Tenant);
        Assert.Equal(1, snapshot.Tenant!.DaysRemaining);
        Assert.NotEqual(999, snapshot.Tenant.DaysRemaining);
        licenseService.Verify(
            s => s.GetLicenseStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyScenario_Expired_SetsPastDate()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Dev",
            Slug = "dev",
            Status = TenantStatuses.Active,
            LicenseValidUntilUtc = DateTime.UtcNow.AddDays(30),
        });
        await db.SaveChangesAsync();

        var storage = new InMemoryLicenseStorage();
        storage.SaveLicenseToFile(new LicensePersistedState
        {
            FirstRunUtc = DateTime.UtcNow,
        });

        var svc = CreateService(db, storage);
        var snapshot = await svc.ApplyScenarioAsync(new LicenseTestScenarioRequest
        {
            TenantId = tenantId,
            Scope = LicenseTestScope.Both,
            Scenario = LicenseTestScenario.Expired,
        });

        Assert.NotNull(snapshot.Tenant);
        Assert.True(snapshot.Tenant!.ValidUntilUtc < DateTime.UtcNow);
        Assert.Equal("expired", snapshot.Tenant.Status);
    }
}
