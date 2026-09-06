using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupRetentionGuardTests
{
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Legal_hold_blocks_delete()
    {
        var run = SystemRun(Now.AddYears(-8));
        run.LegalHold = true;
        Assert.False(BackupRetentionGuard.CanDeleteSucceededRun(run, BackupRetentionPolicySnapshot.Defaults, Now));
    }

    [Fact]
    public void System_run_younger_than_7_years_is_protected()
    {
        var run = SystemRun(Now.AddYears(-3));
        Assert.False(BackupRetentionGuard.CanDeleteSucceededRun(run, BackupRetentionPolicySnapshot.Defaults, Now));
    }

    [Fact]
    public void System_run_older_than_7_years_without_hold_can_delete()
    {
        var run = SystemRun(Now.AddYears(-8));
        Assert.True(BackupRetentionGuard.CanDeleteSucceededRun(run, BackupRetentionPolicySnapshot.Defaults, Now));
    }

    [Fact]
    public void Tenant_run_without_hold_can_delete()
    {
        var run = new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.Tenant,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = Now.AddDays(-40),
            CompletedAt = Now.AddDays(-40)
        };
        Assert.True(BackupRetentionGuard.CanDeleteSucceededRun(run, BackupRetentionPolicySnapshot.Defaults, Now));
    }

    [Fact]
    public void Immutable_artifact_blocks_delete()
    {
        var run = SystemRun(Now.AddYears(-8));
        run.Artifacts.Add(new BackupArtifact
        {
            Id = Guid.NewGuid(),
            BackupRunId = run.Id,
            StorageDescriptor = "dump.dump",
            ImmutableUntilUtc = Now.AddDays(1)
        });
        Assert.False(BackupRetentionGuard.CanDeleteSucceededRun(run, BackupRetentionPolicySnapshot.Defaults, Now));
    }

    private static BackupRun SystemRun(DateTime completed) =>
        new()
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "Fake",
            RequestedAt = completed,
            CompletedAt = completed
        };
}
