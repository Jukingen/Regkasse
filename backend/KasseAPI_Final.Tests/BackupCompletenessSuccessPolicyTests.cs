using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupCompletenessSuccessPolicyTests
{
    [Fact]
    public void PgDump_passed_without_completeness_yields_failure_reason()
    {
        var outcome = new BackupVerificationOutcome(true, false, null, "{}");
        var reason = BackupCompletenessSuccessPolicy.GetIncompleteVerifiedArtifactSetFailureReason(
            BackupExecutionAdapterKind.PgDump,
            outcome);
        Assert.NotNull(reason);
        Assert.Contains("LogicalDump", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Fake_passed_without_completeness_does_not_gate()
    {
        var outcome = new BackupVerificationOutcome(true, false, null, "{}");
        Assert.Null(BackupCompletenessSuccessPolicy.GetIncompleteVerifiedArtifactSetFailureReason(
            BackupExecutionAdapterKind.Fake,
            outcome));
    }

    [Fact]
    public void PgDump_passed_with_completeness_ok()
    {
        var outcome = new BackupVerificationOutcome(true, true, null, "{}");
        Assert.Null(BackupCompletenessSuccessPolicy.GetIncompleteVerifiedArtifactSetFailureReason(
            BackupExecutionAdapterKind.PgDump,
            outcome));
    }

    [Fact]
    public void Format_note_PgDump_mentions_gate()
    {
        var note = BackupCompletenessSuccessPolicy.FormatCompletenessPolicyNote("PgDump");
        Assert.Contains("CompletenessFlag=true", note, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_note_Fake_mentions_informational()
    {
        var note = BackupCompletenessSuccessPolicy.FormatCompletenessPolicyNote("Fake");
        Assert.Contains("informational", note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mapper_sets_completeness_required_on_verification_for_PgDump_run()
    {
        var id = Guid.NewGuid();
        var run = new BackupRun
        {
            Id = id,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            Verifications =
            {
                new BackupVerification
                {
                    BackupRunId = id,
                    Status = BackupVerificationStatus.Passed,
                    StartedAt = DateTime.UtcNow,
                    VerifierSource = "x",
                    CompletenessFlag = true
                }
            }
        };

        var dto = BackupRunMapper.ToDto(run, includeChildren: true, materializedChildren: true);
        Assert.False(string.IsNullOrWhiteSpace(dto.ArtifactCompletenessPolicyNote));
        var v = Assert.Single(dto.Verifications!);
        Assert.True(v.CompletenessRequiredForTerminalSuccess);
    }

    [Fact]
    public void Mapper_sets_completeness_not_required_for_Fake_run()
    {
        var id = Guid.NewGuid();
        var run = new BackupRun
        {
            Id = id,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow,
            Verifications =
            {
                new BackupVerification
                {
                    BackupRunId = id,
                    Status = BackupVerificationStatus.Passed,
                    StartedAt = DateTime.UtcNow,
                    VerifierSource = "x",
                    CompletenessFlag = true
                }
            }
        };

        var dto = BackupRunMapper.ToDto(run, includeChildren: true, materializedChildren: true);
        var v = Assert.Single(dto.Verifications!);
        Assert.False(v.CompletenessRequiredForTerminalSuccess);
    }
}
