using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupRunAccessEvaluatorTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"backup_access_eval_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public void IsRunAccessible_prefers_tenant_id_column()
    {
        var run = new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.Tenant,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            TenantId = TenantA,
            IdempotencyKey = "manual-tenant-other-tenant-1",
        };

        var scope = new BackupRunAccessScope(IsSuperAdmin: false, TenantA, "user-1");
        Assert.True(BackupRunAccessEvaluator.IsRunAccessible(run, scope));
    }

    [Fact]
    public async Task ApplyTenantScopeFilter_matches_tenant_id_column()
    {
        await using var db = CreateDb();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.Tenant,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                TenantId = TenantA,
                RequestedAt = DateTime.UtcNow,
            },
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.Tenant,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                TenantId = Guid.NewGuid(),
                RequestedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var rows = await BackupRunAccessEvaluator
            .ApplyTenantScopeFilter(db.BackupRuns.AsNoTracking(), TenantA)
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal(TenantA, rows[0].TenantId);
    }

    [Fact]
    public void IsRunAccessible_denies_scheduled_system_run_for_manager()
    {
        var run = new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "Fake",
            TenantId = null,
        };

        var scope = new BackupRunAccessScope(IsSuperAdmin: false, TenantA, "manager-1");
        Assert.False(BackupRunAccessEvaluator.IsRunAccessible(run, scope));
    }

    [Fact]
    public void IsRunAccessible_denies_other_tenant_manual_run()
    {
        var otherTenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var run = new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.Tenant,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            TenantId = otherTenant,
        };

        var scope = new BackupRunAccessScope(IsSuperAdmin: false, TenantA, "manager-1");
        Assert.False(BackupRunAccessEvaluator.IsRunAccessible(run, scope));
    }

    [Fact]
    public async Task ApplyCallerAccessFilter_excludes_scheduled_system_runs_for_manager()
    {
        await using var db = CreateDb();
        var scheduledId = Guid.NewGuid();
        var tenantRunId = Guid.NewGuid();
        var otherManualId = Guid.NewGuid();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = scheduledId,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.System,
                TriggerSource = BackupTriggerSource.Scheduled,
                AdapterKind = "Fake",
                TenantId = null,
                RequestedAt = DateTime.UtcNow,
            },
            new BackupRun
            {
                Id = tenantRunId,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.Tenant,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                TenantId = TenantA,
                RequestedAt = DateTime.UtcNow.AddMinutes(-2),
            },
            new BackupRun
            {
                Id = otherManualId,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.Tenant,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                TenantId = Guid.NewGuid(),
                RequestedAt = DateTime.UtcNow.AddMinutes(-1),
            });
        await db.SaveChangesAsync();

        var scope = new BackupRunAccessScope(IsSuperAdmin: false, TenantA, "manager-1");
        var ids = await BackupRunAccessEvaluator
            .ApplyCallerAccessFilter(db.BackupRuns.AsNoTracking(), scope)
            .Select(r => r.Id)
            .ToListAsync();

        Assert.Contains(tenantRunId, ids);
        Assert.DoesNotContain(scheduledId, ids);
        Assert.DoesNotContain(otherManualId, ids);
    }

    [Fact]
    public async Task ApplyActiveManualConflictScope_isolates_tenants()
    {
        var tenantA = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        await using var db = CreateDb();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Queued,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                TenantId = tenantA,
                RequestedAt = DateTime.UtcNow,
            },
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Queued,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                TenantId = tenantB,
                RequestedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var tenantAActive = await BackupRunAccessEvaluator
            .ApplyActiveManualConflictScope(db.BackupRuns.AsNoTracking(), tenantA)
            .CountAsync();
        var tenantBActive = await BackupRunAccessEvaluator
            .ApplyActiveManualConflictScope(db.BackupRuns.AsNoTracking(), tenantB)
            .CountAsync();

        Assert.Equal(1, tenantAActive);
        Assert.Equal(1, tenantBActive);
    }

    [Fact]
    public async Task GetLatestVerificationAsync_scopes_to_accessible_runs()
    {
        var tenantA = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        await using var db = CreateDb();
        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();
        var verA = Guid.NewGuid();
        var verB = Guid.NewGuid();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = runA,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.Tenant,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                TenantId = tenantA,
                RequestedAt = DateTime.UtcNow.AddHours(-2),
            },
            new BackupRun
            {
                Id = runB,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.Tenant,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                TenantId = tenantB,
                RequestedAt = DateTime.UtcNow.AddHours(-1),
            });
        db.BackupVerifications.AddRange(
            new BackupVerification
            {
                Id = verA,
                BackupRunId = runA,
                Status = BackupVerificationStatus.Passed,
                StartedAt = DateTime.UtcNow.AddHours(-1),
                VerifierSource = "test",
            },
            new BackupVerification
            {
                Id = verB,
                BackupRunId = runB,
                Status = BackupVerificationStatus.Passed,
                StartedAt = DateTime.UtcNow,
                VerifierSource = "test",
            });
        await db.SaveChangesAsync();

        var svc = new BackupRunQueryService(db);
        var scope = new BackupRunAccessScope(IsSuperAdmin: false, tenantA, "manager-1");
        var latest = await svc.GetLatestVerificationAsync(scope);

        Assert.NotNull(latest);
        Assert.Equal(verA, latest!.Id);
        Assert.Equal(runA, latest.BackupRunId);
    }
}
