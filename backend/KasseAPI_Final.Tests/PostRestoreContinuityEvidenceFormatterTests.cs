using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PostRestoreContinuityEvidenceFormatterTests
{
    [Fact]
    public void MapProofState_inconclusive_when_required_failed_only_by_inconclusive_status()
    {
        var outcome = new PostRestoreDrillSqlOutcome
        {
            Executed = true,
            Passed = false,
            Checks =
            [
                new PostRestoreSqlCheckRow
                {
                    Id = "x",
                    Name = "x",
                    Category = "platform",
                    Status = PostRestoreSqlCheckStatus.Inconclusive,
                    Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                    ReasonCode = PostRestoreSqlReasonCodes.MissingTargetConnection,
                    FailureCategory = PostRestoreSqlFailureCategory.Unknown,
                    Summary = "n"
                }
            ]
        };

        Assert.Equal(PostRestoreContinuityProofState.Inconclusive, PostRestoreContinuityEvidenceFormatter.MapProofState(outcome));
    }

    [Fact]
    public void MapProofState_failed_when_any_required_failed()
    {
        var outcome = new PostRestoreDrillSqlOutcome
        {
            Executed = true,
            Passed = false,
            Checks =
            [
                new PostRestoreSqlCheckRow
                {
                    Id = "a",
                    Name = "a",
                    Category = "fiscal_spine",
                    Status = PostRestoreSqlCheckStatus.Failed,
                    Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                    ReasonCode = PostRestoreSqlReasonCodes.SchemaRelationMissing,
                    FailureCategory = PostRestoreSqlFailureCategory.MissingTable,
                    Summary = "missing"
                },
                new PostRestoreSqlCheckRow
                {
                    Id = "b",
                    Name = "b",
                    Category = "platform",
                    Status = PostRestoreSqlCheckStatus.Inconclusive,
                    Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                    ReasonCode = PostRestoreSqlReasonCodes.MissingTargetConnection,
                    FailureCategory = PostRestoreSqlFailureCategory.Unknown,
                    Summary = "n"
                }
            ]
        };

        Assert.Equal(PostRestoreContinuityProofState.Failed, PostRestoreContinuityEvidenceFormatter.MapProofState(outcome));
    }
}
