using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreProofMilestonesQueryServiceTests
{
    [Fact]
    public async Task Latest_drill_failure_does_not_remove_older_L4_last_known_good()
    {
        var dbName = $"rv_milestones_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));

        var l4Good = Guid.NewGuid();
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Id = l4Good,
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            RequestedAt = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 4, 1, 10, 5, 0, DateTimeKind.Utc),
            PostRestoreContinuityChecksExecuted = true,
            PostRestoreContinuityChecksPassed = true
        });

        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Id = Guid.NewGuid(),
            Status = RestoreVerificationStatus.Failed,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            RequestedAt = new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 4, 2, 10, 1, 0, DateTimeKind.Utc),
            PostRestoreContinuityChecksExecuted = true,
            PostRestoreContinuityChecksPassed = false
        });

        await db.SaveChangesAsync();

        var sut = new RestoreProofMilestonesQueryService(db);
        var m = await sut.GetMilestonesAsync();

        Assert.NotNull(m.LastKnownGoodL4ContinuityProven);
        Assert.Equal(l4Good, m.LastKnownGoodL4ContinuityProven!.Id);
        Assert.Contains("LATEST_DRILL_ATTEMPT_FAILED", m.Semantics.WarningCodes);
    }

    [Fact]
    public async Task Newer_success_without_L4_triggers_semantic_warning_while_keeping_older_L4()
    {
        var dbName = $"rv_milestones2_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));

        var l4Old = Guid.NewGuid();
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Id = l4Old,
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            RequestedAt = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 4, 1, 8, 10, 0, DateTimeKind.Utc),
            PostRestoreContinuityChecksExecuted = true,
            PostRestoreContinuityChecksPassed = true
        });

        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Id = Guid.NewGuid(),
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            RequestedAt = new DateTime(2026, 4, 3, 9, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 4, 3, 9, 10, 0, DateTimeKind.Utc),
            PostRestoreContinuityChecksExecuted = false,
            PostRestoreContinuityChecksPassed = null
        });

        await db.SaveChangesAsync();

        var sut = new RestoreProofMilestonesQueryService(db);
        var m = await sut.GetMilestonesAsync();

        Assert.Equal(l4Old, m.LastKnownGoodL4ContinuityProven?.Id);
        Assert.Contains("NEWER_DRILL_SUCCESS_WITHOUT_L4_CONTINUITY_PROOF", m.Semantics.WarningCodes);
    }
}
