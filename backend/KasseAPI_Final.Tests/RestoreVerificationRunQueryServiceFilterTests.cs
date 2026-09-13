using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreVerificationRunQueryServiceFilterTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rv_query_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task GetHistoryAsync_Filters_By_Status_Type_Date_And_BackupId()
    {
        await using var db = CreateDb();
        var backupA = AddBackup(db, TenantA, BackupStrategyKind.Tenant);
        var backupB = AddBackup(db, TenantB, BackupStrategyKind.Tenant);
        var older = AddDrill(
            db,
            RestoreVerificationStatus.Succeeded,
            RestoreVerificationTriggerSource.Scheduled,
            backupA.Id,
            DateTime.UtcNow.AddDays(-3));
        var newer = AddDrill(
            db,
            RestoreVerificationStatus.Failed,
            RestoreVerificationTriggerSource.Manual,
            backupB.Id,
            DateTime.UtcNow.AddHours(-1));
        await db.SaveChangesAsync();

        var svc = new RestoreVerificationRunQueryService(db);

        var byStatus = await svc.GetHistoryAsync(
            1,
            20,
            new RestoreVerificationHistoryFilter { Status = RestoreVerificationStatus.Failed },
            access: null);
        Assert.Equal(1, byStatus.TotalCount);
        Assert.Equal(newer.Id, byStatus.Items[0].Id);

        var byType = await svc.GetHistoryAsync(
            1,
            20,
            new RestoreVerificationHistoryFilter { TriggerSource = RestoreVerificationTriggerSource.Scheduled },
            access: null);
        Assert.Equal(1, byType.TotalCount);
        Assert.Equal(older.Id, byType.Items[0].Id);

        var byDate = await svc.GetHistoryAsync(
            1,
            20,
            new RestoreVerificationHistoryFilter { FromUtc = DateTime.UtcNow.AddDays(-1) },
            access: null);
        Assert.Equal(1, byDate.TotalCount);
        Assert.Equal(newer.Id, byDate.Items[0].Id);

        var byBackup = await svc.GetHistoryAsync(
            1,
            20,
            new RestoreVerificationHistoryFilter { SourceBackupRunIdSearch = backupA.Id.ToString("D") },
            access: null);
        Assert.Equal(1, byBackup.TotalCount);
        Assert.Equal(older.Id, byBackup.Items[0].Id);
    }

    [Fact]
    public async Task GetHistoryAsync_Manager_Sees_Only_Own_Tenant_Strategy_Runs()
    {
        await using var db = CreateDb();
        var tenantBackup = AddBackup(db, TenantA, BackupStrategyKind.Tenant);
        var otherTenant = AddBackup(db, TenantB, BackupStrategyKind.Tenant);
        var systemBackup = AddBackup(db, tenantId: null, BackupStrategyKind.System);
        var visible = AddDrill(db, RestoreVerificationStatus.Succeeded, RestoreVerificationTriggerSource.Manual, tenantBackup.Id, DateTime.UtcNow);
        AddDrill(db, RestoreVerificationStatus.Succeeded, RestoreVerificationTriggerSource.Manual, otherTenant.Id, DateTime.UtcNow);
        var hiddenSystem = AddDrill(db, RestoreVerificationStatus.Succeeded, RestoreVerificationTriggerSource.Scheduled, systemBackup.Id, DateTime.UtcNow);
        await db.SaveChangesAsync();

        var svc = new RestoreVerificationRunQueryService(db);
        var scope = new RestoreVerificationAccessScope(IsSuperAdmin: false, TenantA);
        var (items, total) = await svc.GetHistoryAsync(1, 20, filter: null, scope);

        Assert.Equal(1, total);
        Assert.Equal(visible.Id, Assert.Single(items).Id);
        Assert.Null(await svc.GetByIdAsync(hiddenSystem.Id, scope));
    }

    [Fact]
    public async Task GetHistoryAsync_SuperAdmin_Sees_System_Drills()
    {
        await using var db = CreateDb();
        var systemBackup = AddBackup(db, tenantId: null, BackupStrategyKind.System);
        var drill = AddDrill(db, RestoreVerificationStatus.Succeeded, RestoreVerificationTriggerSource.Scheduled, systemBackup.Id, DateTime.UtcNow);
        await db.SaveChangesAsync();

        var svc = new RestoreVerificationRunQueryService(db);
        var scope = new RestoreVerificationAccessScope(IsSuperAdmin: true, TenantA);
        var (items, total) = await svc.GetHistoryAsync(1, 20, filter: null, scope);

        Assert.Equal(1, total);
        Assert.Equal(drill.Id, Assert.Single(items).Id);
    }

    private static BackupRun AddBackup(AppDbContext db, Guid? tenantId, BackupStrategyKind strategy)
    {
        var run = new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            Strategy = strategy,
            TenantId = tenantId,
            RequestedAt = DateTime.UtcNow
        };
        db.BackupRuns.Add(run);
        return run;
    }

    private static RestoreVerificationRun AddDrill(
        AppDbContext db,
        RestoreVerificationStatus status,
        RestoreVerificationTriggerSource trigger,
        Guid backupId,
        DateTime requestedAt)
    {
        var run = new RestoreVerificationRun
        {
            Id = Guid.NewGuid(),
            Status = status,
            TriggerSource = trigger,
            SourceBackupRunId = backupId,
            RequestedAt = requestedAt
        };
        db.RestoreVerificationRuns.Add(run);
        return run;
    }
}
