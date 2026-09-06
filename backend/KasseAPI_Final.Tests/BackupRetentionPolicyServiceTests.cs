using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupRetentionPolicyServiceTests
{
    [Fact]
    public async Task Get_creates_singleton_with_legal_defaults()
    {
        await using var db = CreateDb(nameof(Get_creates_singleton_with_legal_defaults));
        var sut = CreateSut(db);

        var dto = await sut.GetAsync(includeCosts: false, accessScope: null);

        Assert.Equal(30, dto.HotRetentionDays);
        Assert.Equal(90, dto.WarmRetentionDays);
        Assert.Equal(7, dto.ColdRetentionYears);
        Assert.True(dto.LegalRetentionEnforced);
        Assert.False(dto.ColdStorageEnabled);
        Assert.Equal(1, await db.BackupRetentionPolicySettings.CountAsync());
    }

    [Fact]
    public async Task Update_rejects_cold_years_below_7()
    {
        await using var db = CreateDb(nameof(Update_rejects_cold_years_below_7));
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.UpdateAsync(
                new BackupRetentionPolicyPutRequestDto
                {
                    HotRetentionDays = 30,
                    WarmRetentionDays = 90,
                    ColdRetentionYears = 5,
                    LegalRetentionEnforced = true
                },
                "user-1",
                Roles.SuperAdmin));
    }

    [Fact]
    public async Task Update_rejects_disabling_legal_retention()
    {
        await using var db = CreateDb(nameof(Update_rejects_disabling_legal_retention));
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.UpdateAsync(
                new BackupRetentionPolicyPutRequestDto
                {
                    HotRetentionDays = 30,
                    WarmRetentionDays = 90,
                    ColdRetentionYears = 7,
                    LegalRetentionEnforced = false
                },
                "user-1",
                Roles.SuperAdmin));
    }

    [Fact]
    public async Task Apply_legal_hold_on_system_success()
    {
        await using var db = CreateDb(nameof(Apply_legal_hold_on_system_success));
        var sut = CreateSut(db);
        var completed = DateTime.UtcNow.AddDays(-1);
        var run = new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "Fake",
            RequestedAt = completed,
            CompletedAt = completed
        };

        await sut.ApplyDefaultLegalHoldIfRequiredAsync(run, "system");

        Assert.True(run.LegalHold);
        Assert.Equal(completed.AddYears(7), run.LegalHoldUntilUtc);
        Assert.Contains("BAO", run.LegalHoldReason, StringComparison.Ordinal);
    }

    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options,
            TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));

    private static BackupRetentionPolicyService CreateSut(AppDbContext db)
    {
        var opts = new Mock<IOptionsMonitor<BackupOptions>>();
        opts.Setup(o => o.CurrentValue).Returns(new BackupOptions());
        var cloud = new Mock<ICloudStorageService>();
        cloud.SetupGet(c => c.IsConfigured).Returns(false);
        cloud.SetupGet(c => c.ResolvedProvider).Returns(CloudStorageProviderKind.Filesystem);
        var costs = new Mock<IBackupStorageCostService>();
        costs.Setup(c => c.GetAsync(It.IsAny<BackupRunAccessScope?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupStorageCostResponseDto());
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        return new BackupRetentionPolicyService(
            db,
            opts.Object,
            cloud.Object,
            costs.Object,
            audit.Object,
            NullLogger<BackupRetentionPolicyService>.Instance);
    }
}
