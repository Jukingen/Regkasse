using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_ReportsCatalogGaps()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);

        var svc = CreateService(db);
        var status = await svc.CheckForUpdatesAsync(tenantId);

        Assert.True(status.HasUpdates);
        Assert.NotEmpty(status.AvailableUpdates);
        Assert.True(status.DiagnosticOnly);
        Assert.Contains(status.AvailableUpdates, u => u.UpdateType == TseUpdateTypes.HealthProbePolicy);
    }

    [Fact]
    public async Task ApplyUpdateAsync_LowRisk_SucceedsWithoutBackup()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithPrimaryAsync(db);

        var svc = CreateService(db);
        var result = await svc.ApplyUpdateAsync(tenantId, TseUpdateTypes.HealthProbePolicy, "actor-1");

        Assert.True(result.Success);
        Assert.Equal(TseUpdateRunStatuses.Succeeded, result.Status);
        Assert.True(result.ZeroDowntime);
        Assert.Equal("0.0.0", result.FromVersion);
        Assert.NotEqual("0.0.0", result.ToVersion);

        var status = await svc.CheckForUpdatesAsync(tenantId);
        Assert.DoesNotContain(status.AvailableUpdates, u => u.UpdateType == TseUpdateTypes.HealthProbePolicy);

        var history = await svc.GetUpdateHistoryAsync(tenantId);
        Assert.Contains(history.Items, i => i.UpdateType == TseUpdateTypes.HealthProbePolicy && i.Status == TseUpdateRunStatuses.Succeeded);
    }

    [Fact]
    public async Task ApplyUpdateAsync_MediumRiskWithoutBackup_IsBlocked()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantWithPrimaryAsync(db);

        var svc = CreateService(db);
        var result = await svc.ApplyUpdateAsync(tenantId, TseUpdateTypes.FailoverPolicy, "actor-1");

        Assert.False(result.Success);
        Assert.Equal(TseUpdateRunStatuses.Blocked, result.Status);
        Assert.Contains("healthy backup", result.Message, StringComparison.OrdinalIgnoreCase);

        var history = await svc.GetUpdateHistoryAsync(tenantId);
        Assert.Contains(history.Items, i => i.Status == TseUpdateRunStatuses.Blocked);
    }

    [Fact]
    public async Task ApplyUpdateAsync_MediumRiskWithBackup_Succeeds()
    {
        await using var db = CreateDb();
        var (tenantId, registerId) = await SeedTenantWithPrimaryReturningRegisterAsync(db);
        db.TseDevices.Add(Backup(tenantId, registerId, "UPD-B1"));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.ApplyUpdateAsync(tenantId, TseUpdateTypes.FailoverPolicy, "actor-1");

        Assert.True(result.Success);
        Assert.Equal(TseUpdateRunStatuses.Succeeded, result.Status);
        Assert.True(result.DevicesTouched >= 2);
    }

    private static TseUpdateService CreateService(AppDbContext db) =>
        new(
            db,
            Options.Create(new TseOptions { FailoverHealthyMinScore = 80 }).ToMonitor(),
            NullLogger<TseUpdateService>.Instance);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_upd_{Guid.NewGuid():N}")
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
            Name = "Update Cafe",
            Slug = "update-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static async Task<Guid> SeedTenantWithPrimaryAsync(AppDbContext db)
    {
        var (tenantId, _) = await SeedTenantWithPrimaryReturningRegisterAsync(db);
        return tenantId;
    }

    private static async Task<(Guid TenantId, Guid RegisterId)> SeedTenantWithPrimaryReturningRegisterAsync(
        AppDbContext db)
    {
        var tenantId = await SeedTenantAsync(db);
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = "KASSE-U1",
            Location = "Haupt",
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();

        db.TseDevices.Add(new TseDevice
        {
            SerialNumber = "UPD-P1",
            DeviceType = "Cloud",
            VendorId = "auto",
            ProductId = "fiskaly",
            Provider = TseOptions.ProviderFiskaly,
            TenantId = tenantId,
            CashRegisterId = register.Id,
            KassenId = register.Id,
            DeviceId = "upd-primary",
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = true,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 95,
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
            DeviceId = serial.ToLowerInvariant(),
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = false,
            IsBackup = true,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 100,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
}
