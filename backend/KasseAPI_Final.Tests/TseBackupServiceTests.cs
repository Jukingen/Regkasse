using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseBackupServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_backup_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TseBackupService CreateService(AppDbContext db)
    {
        var backupEnc = new Mock<IBackupEncryptionService>();
        backupEnc.SetupGet(e => e.IsEnabled).Returns(false);

        var dp = DataProtectionProvider.Create("TseBackupServiceTests");
        return new TseBackupService(
            db,
            backupEnc.Object,
            dp,
            Mock.Of<IAuditLogService>(),
            NullLogger<TseBackupService>.Instance);
    }

    private static async Task<(Guid TenantId, Guid RegisterId, Guid DeviceId)> SeedAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Backup Cafe",
            Slug = "backup-cafe",
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

        var device = new TseDevice
        {
            SerialNumber = "SER-1",
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            KassenId = register.Id,
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
        db.TseDevices.Add(device);
        db.SignatureChainState.Add(new SignatureChainState
        {
            TenantId = tenantId,
            CashRegisterId = register.Id,
            LastCounter = 3,
            LastSignature = "abc.def.ghi",
            LastTurnoverCounterCents = 1500,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return (tenantId, register.Id, device.Id);
    }

    [Fact]
    public async Task CreateTseBackupAsync_StoresEncryptedSnapshot()
    {
        await using var db = CreateDb();
        var (tenantId, _, _) = await SeedAsync(db);
        var svc = CreateService(db);

        var result = await svc.CreateTseBackupAsync(tenantId, "admin-1", notes: "nightly");
        Assert.True(result.Success);
        Assert.NotNull(result.BackupId);
        Assert.Equal(1, result.Backup!.DeviceCount);
        Assert.Equal(1, result.Backup.ChainCount);
        Assert.Equal(TseBackupService.EncryptionDataProtection, result.Backup.EncryptionKind);

        var row = await db.TseBackups.IgnoreQueryFilters().SingleAsync();
        Assert.NotEmpty(row.Payload);
        Assert.DoesNotContain("abc.def.ghi", System.Text.Encoding.UTF8.GetString(row.Payload));
    }

    [Fact]
    public async Task RestoreTseBackupAsync_RequiresConfirmToken()
    {
        await using var db = CreateDb();
        var (tenantId, _, _) = await SeedAsync(db);
        var svc = CreateService(db);
        var created = await svc.CreateTseBackupAsync(tenantId, "admin-1");

        var denied = await svc.RestoreTseBackupAsync(
            created.BackupId!.Value,
            new RestoreTseBackupRequestDto { ConfirmToken = "nope" },
            "admin-1");
        Assert.False(denied.Success);
    }

    [Fact]
    public async Task RestoreTseBackupAsync_RestoresChainAfterWipe()
    {
        await using var db = CreateDb();
        var (tenantId, registerId, _) = await SeedAsync(db);
        var svc = CreateService(db);
        var created = await svc.CreateTseBackupAsync(tenantId, "admin-1");

        var chain = await db.SignatureChainState.IgnoreQueryFilters()
            .SingleAsync(c => c.CashRegisterId == registerId);
        chain.LastCounter = 0;
        chain.LastSignature = null;
        chain.LastTurnoverCounterCents = 0;
        await db.SaveChangesAsync();

        var restored = await svc.RestoreTseBackupAsync(
            created.BackupId!.Value,
            new RestoreTseBackupRequestDto { ConfirmToken = TseBackupService.ConfirmTokenValue },
            "admin-1");

        Assert.True(restored.Success);
        Assert.Equal(1, restored.ChainsUpserted);

        var after = await db.SignatureChainState.IgnoreQueryFilters()
            .SingleAsync(c => c.CashRegisterId == registerId);
        Assert.Equal(3, after.LastCounter);
        Assert.Equal("abc.def.ghi", after.LastSignature);
        Assert.Equal(1500, after.LastTurnoverCounterCents);
    }

    [Fact]
    public async Task RestoreTseBackupAsync_SkipsDowngradeWithoutForce()
    {
        await using var db = CreateDb();
        var (tenantId, registerId, _) = await SeedAsync(db);
        var svc = CreateService(db);
        var created = await svc.CreateTseBackupAsync(tenantId, "admin-1");

        var chain = await db.SignatureChainState.IgnoreQueryFilters()
            .SingleAsync(c => c.CashRegisterId == registerId);
        chain.LastCounter = 99;
        await db.SaveChangesAsync();

        var restored = await svc.RestoreTseBackupAsync(
            created.BackupId!.Value,
            new RestoreTseBackupRequestDto { ConfirmToken = TseBackupService.ConfirmTokenValue },
            "admin-1");

        Assert.True(restored.Success);
        Assert.Equal(0, restored.ChainsUpserted);
        Assert.Equal(1, restored.ChainsSkipped);

        var after = await db.SignatureChainState.IgnoreQueryFilters()
            .SingleAsync(c => c.CashRegisterId == registerId);
        Assert.Equal(99, after.LastCounter);
    }

    [Fact]
    public async Task PreviewRestoreAsync_DetectsForceNeeded()
    {
        await using var db = CreateDb();
        var (tenantId, registerId, _) = await SeedAsync(db);
        var svc = CreateService(db);
        var created = await svc.CreateTseBackupAsync(tenantId, "admin-1");

        var chain = await db.SignatureChainState.IgnoreQueryFilters()
            .SingleAsync(c => c.CashRegisterId == registerId);
        chain.LastCounter = 50;
        await db.SaveChangesAsync();

        var preview = await svc.PreviewRestoreAsync(created.BackupId!.Value);
        Assert.NotNull(preview);
        Assert.True(preview!.WouldRequireForceDowngrade);
    }

    [Fact]
    public async Task CreateTseBackupAsync_IncludesFailoverFields_AndTenantScopedDevices()
    {
        await using var db = CreateDb();
        var (tenantId, registerId, primaryId) = await SeedAsync(db);

        // Tenant-scoped primary + backup (failover registry)
        var primary = await db.TseDevices.FindAsync(primaryId);
        primary!.TenantId = tenantId;
        primary.CashRegisterId = registerId;
        primary.DeviceId = "vendor-primary";
        primary.Provider = "fiskaly";
        primary.IsPrimary = true;
        primary.IsBackup = false;
        primary.ApiKey = "cipher-key";
        primary.Certificate = "thumbprint-abc";

        var backup = new TseDevice
        {
            SerialNumber = "SER-BACKUP",
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            CashRegisterId = registerId,
            KassenId = registerId,
            DeviceId = "vendor-backup",
            Provider = "fiskaly",
            IsConnected = true,
            CanCreateInvoices = true,
            IsActive = true,
            IsPrimary = false,
            IsBackup = true,
            PrimaryDeviceId = primaryId,
            IsFailoverActive = true,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 90,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
        db.TseDevices.Add(backup);
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = tenantId,
            CompanyName = "Backup Cafe",
            CompanyAddress = "Test 1",
            CompanyTaxNumber = "ATU12345678",
            DefaultTseDeviceId = primaryId.ToString("D"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var result = await svc.CreateTseBackupAsync(tenantId, "admin-1", notes: "full");
        Assert.True(result.Success);
        Assert.Equal(2, result.Backup!.DeviceCount);
        Assert.Equal(2, result.Backup.SchemaVersion);

        // Wipe live failover + default signer, then restore
        backup.IsFailoverActive = false;
        primary.ApiKey = null;
        primary.Certificate = null;
        var settings = await db.CompanySettings.IgnoreQueryFilters().SingleAsync(s => s.TenantId == tenantId);
        settings.DefaultTseDeviceId = primaryId.ToString("D");
        await db.SaveChangesAsync();

        var restored = await svc.RestoreTseBackupAsync(
            result.BackupId!.Value,
            new RestoreTseBackupRequestDto { ConfirmToken = TseBackupService.ConfirmTokenValue },
            "admin-1");
        Assert.True(restored.Success);
        Assert.Equal(2, restored.DevicesUpserted);

        await db.Entry(backup).ReloadAsync();
        await db.Entry(primary).ReloadAsync();
        await db.Entry(settings).ReloadAsync();

        Assert.True(backup.IsFailoverActive);
        Assert.True(backup.IsBackup);
        Assert.Equal(primaryId, backup.PrimaryDeviceId);
        Assert.Equal("cipher-key", primary.ApiKey);
        Assert.Equal("thumbprint-abc", primary.Certificate);
        Assert.Equal(backup.Id.ToString("D"), settings.DefaultTseDeviceId);
    }

    [Fact]
    public async Task ITseFullBackupService_CreateFullBackup_Delegates()
    {
        await using var db = CreateDb();
        var (tenantId, _, _) = await SeedAsync(db);
        ITseFullBackupService full = CreateService(db);

        var created = await full.CreateFullBackupAsync(tenantId, "admin-1");
        Assert.True(created.Success);

        var list = await full.ListBackupsAsync(tenantId);
        Assert.Single(list);
    }
}
