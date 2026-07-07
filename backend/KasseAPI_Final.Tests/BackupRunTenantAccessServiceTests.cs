using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupRunTenantAccessServiceTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"backup_tenant_access_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task SuperAdmin_without_tenant_sees_any_run()
    {
        await using var db = CreateDb();
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            IdempotencyKey = "manual-tenant-other-1",
        });
        await db.SaveChangesAsync();

        var svc = new BackupRunTenantAccessService(db);
        var run = await svc.TryGetAccessibleRunAsync(runId, isSuperAdmin: true, callerTenantId: null, cancellationToken: CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal(runId, run!.Id);
    }

    [Fact]
    public async Task Manager_only_sees_matching_tenant_run()
    {
        var tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await using var db = CreateDb();
        var allowedId = Guid.NewGuid();
        var deniedId = Guid.NewGuid();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = allowedId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                IdempotencyKey = $"manual-tenant-{tenantId:D}-1",
            },
            new BackupRun
            {
                Id = deniedId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                IdempotencyKey = "manual-tenant-other-tenant-2",
            });
        await db.SaveChangesAsync();

        var svc = new BackupRunTenantAccessService(db);
        var allowed = await svc.TryGetAccessibleRunAsync(allowedId, false, tenantId, cancellationToken: CancellationToken.None);
        var denied = await svc.TryGetAccessibleRunAsync(deniedId, false, tenantId, cancellationToken: CancellationToken.None);

        Assert.NotNull(allowed);
        Assert.Null(denied);
    }

    [Fact]
    public async Task Manager_legacy_manual_run_without_tenant_key_allowed_for_requester()
    {
        var tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        const string managerUserId = "manager-1";
        await using var db = CreateDb();
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            IdempotencyKey = null,
            RequestedByUserId = managerUserId,
        });
        await db.SaveChangesAsync();

        var svc = new BackupRunTenantAccessService(db);
        var allowed = await svc.TryGetAccessibleRunAsync(
            runId,
            isSuperAdmin: false,
            tenantId,
            managerUserId,
            CancellationToken.None);
        var denied = await svc.TryGetAccessibleRunAsync(
            runId,
            isSuperAdmin: false,
            tenantId,
            "other-manager",
            CancellationToken.None);

        Assert.NotNull(allowed);
        Assert.Null(denied);
    }

    [Fact]
    public async Task Manager_sees_run_with_tenant_id_column()
    {
        var tenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        await using var db = CreateDb();
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            TenantId = tenantId,
            IdempotencyKey = "legacy-key-without-tenant-encoding",
        });
        await db.SaveChangesAsync();

        var svc = new BackupRunTenantAccessService(db);
        var allowed = await svc.TryGetAccessibleRunAsync(runId, false, tenantId, cancellationToken: CancellationToken.None);
        var denied = await svc.TryGetAccessibleRunAsync(runId, false, Guid.NewGuid(), cancellationToken: CancellationToken.None);

        Assert.NotNull(allowed);
        Assert.Null(denied);
    }

    [Fact]
    public async Task Import_key_matches_tenant_hint()
    {
        var tenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        await using var db = CreateDb();
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.OperatorApi,
            AdapterKind = BackupArtifactImportService.ImportedAdapterKind,
            IdempotencyKey = $"import-tenant-{tenantId:D}-{DateTime.UtcNow.Ticks}",
        });
        await db.SaveChangesAsync();

        var svc = new BackupRunTenantAccessService(db);
        var run = await svc.TryGetAccessibleRunAsync(runId, false, tenantId, cancellationToken: CancellationToken.None);
        Assert.NotNull(run);
    }
}
