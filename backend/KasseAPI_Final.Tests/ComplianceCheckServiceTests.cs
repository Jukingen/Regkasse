using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ComplianceCheckServiceTests
{
    [Fact]
    public async Task CheckRestoreComplianceAsync_fails_when_backup_missing()
    {
        var (sut, _) = CreateSut(nameof(CheckRestoreComplianceAsync_fails_when_backup_missing));
        var result = await sut.CheckRestoreComplianceAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result.Succeeded);
        Assert.Equal(ComplianceCheckService.BackupNotFoundCode, result.Code);
    }

    [Fact]
    public async Task CheckRestoreComplianceAsync_fails_cross_tenant()
    {
        var (sut, db) = CreateSut(nameof(CheckRestoreComplianceAsync_fails_cross_tenant));
        var backupId = Guid.NewGuid();
        SeedSystemDump(db, backupId, tenantId: Guid.NewGuid());
        await db.SaveChangesAsync();

        var result = await sut.CheckRestoreComplianceAsync(backupId, Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(RestoreService.CrossTenantCode, result.Code);
        Assert.Contains(result.Checks, c => c.Name == ComplianceCheckService.CheckSameTenant && !c.Passed);
    }

    [Fact]
    public async Task CheckRestoreComplianceAsync_fails_missing_hash()
    {
        var (sut, db) = CreateSut(nameof(CheckRestoreComplianceAsync_fails_missing_hash));
        var backupId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            TenantId = tenantId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            Strategy = BackupStrategyKind.System,
            RequestedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = "nohash.dump",
                    ContentHashSha256 = null,
                    ByteSize = 1
                }
            }
        });
        await db.SaveChangesAsync();

        var result = await sut.CheckRestoreComplianceAsync(backupId, tenantId);

        Assert.False(result.Succeeded);
        Assert.Equal(ComplianceCheckService.IntegrityHashMissingCode, result.Code);
        Assert.Contains(result.Checks, c => c.Name == ComplianceCheckService.CheckBackupIntegrity && !c.Passed);
    }

    [Fact]
    public async Task CheckRestoreComplianceAsync_rejects_tenant_zip_strategy()
    {
        var (sut, db) = CreateSut(nameof(CheckRestoreComplianceAsync_rejects_tenant_zip_strategy));
        var backupId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            TenantId = tenantId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "TenantZip",
            Strategy = BackupStrategyKind.Tenant,
            RequestedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = "t.tenant.zip",
                    ContentHashSha256 = new string('c', 64),
                    ByteSize = 5
                }
            }
        });
        await db.SaveChangesAsync();

        var result = await sut.CheckRestoreComplianceAsync(backupId, tenantId);

        Assert.False(result.Succeeded);
        Assert.Equal(ComplianceCheckService.TenantPackageRestoreCode, result.Code);
    }

    [Fact]
    public async Task CheckRestoreComplianceAsync_succeeds_for_same_tenant_system_dump()
    {
        var (sut, db) = CreateSut(nameof(CheckRestoreComplianceAsync_succeeds_for_same_tenant_system_dump));
        var backupId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        SeedSystemDump(db, backupId, tenantId);
        await db.SaveChangesAsync();

        var result = await sut.CheckRestoreComplianceAsync(backupId, tenantId);

        Assert.True(result.Succeeded);
        Assert.All(result.Checks, c => Assert.True(c.Passed));
        Assert.Contains(result.Checks, c => c.Name == ComplianceCheckService.CheckSameTenant);
        Assert.Contains(result.Checks, c => c.Name == ComplianceCheckService.CheckBackupIntegrity);
        Assert.Contains(result.Checks, c => c.Name == ComplianceCheckService.CheckRksvGate);
    }

    private static void SeedSystemDump(AppDbContext db, Guid backupId, Guid? tenantId)
    {
        db.BackupRuns.Add(new BackupRun
        {
            Id = backupId,
            TenantId = tenantId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            Strategy = BackupStrategyKind.System,
            RequestedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = $"{backupId:N}.dump",
                    ContentHashSha256 = new string('b', 64),
                    ByteSize = 100
                }
            }
        });
    }

    private static (ComplianceCheckService Sut, AppDbContext Db) CreateSut(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var checksum = new Mock<IBackupChecksumService>();
        checksum.Setup(c => c.FileMatchesSha256Async(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new ComplianceCheckService(
            db,
            new RestoreService(),
            checksum.Object,
            new FixedOptionsMonitor(new BackupOptions { ArtifactStagingRoot = null }),
            NullLogger<ComplianceCheckService>.Instance);

        return (sut, db);
    }

    private sealed class FixedOptionsMonitor : IOptionsMonitor<BackupOptions>
    {
        public FixedOptionsMonitor(BackupOptions value) => CurrentValue = value;
        public BackupOptions CurrentValue { get; }
        public BackupOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<BackupOptions, string?> listener) => null;
    }
}
