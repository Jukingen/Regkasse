using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PostRestoreDrillSqlCheckerL4Tests
{
    [Fact]
    public void ComputeL4LayerPass_true_when_only_informative_present()
    {
        var checks = new PostRestoreSqlCheckRow[]
        {
            new()
            {
                Id = "x",
                Name = "n",
                Category = "informative",
                Status = PostRestoreSqlCheckStatus.Passed,
                Severity = PostRestoreSqlCheckSeverity.Informative,
                ReasonCode = PostRestoreSqlReasonCodes.DatasetSizeNote,
                FailureCategory = PostRestoreSqlFailureCategory.None,
                Summary = "s"
            }
        };

        Assert.True(PostRestoreDrillSqlChecker.ComputeL4LayerPass(checks));
    }

    [Fact]
    public void ComputeL4LayerPass_false_when_required_inconclusive()
    {
        var checks = new PostRestoreSqlCheckRow[]
        {
            new()
            {
                Id = "a",
                Name = "n",
                Category = "platform",
                Status = PostRestoreSqlCheckStatus.Inconclusive,
                Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                ReasonCode = PostRestoreSqlReasonCodes.TableQueryFailed,
                FailureCategory = PostRestoreSqlFailureCategory.QueryFailed,
                Summary = "s"
            }
        };

        Assert.False(PostRestoreDrillSqlChecker.ComputeL4LayerPass(checks));
    }

    [Fact]
    public void ComputeL4LayerPass_true_when_required_passed_and_informative_failed_hypothetical()
    {
        var checks = new PostRestoreSqlCheckRow[]
        {
            new()
            {
                Id = "r",
                Name = "n",
                Category = "platform",
                Status = PostRestoreSqlCheckStatus.Passed,
                Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                ReasonCode = PostRestoreSqlReasonCodes.TableAccessible,
                FailureCategory = PostRestoreSqlFailureCategory.None,
                Summary = "ok"
            },
            new()
            {
                Id = "i",
                Name = "n",
                Category = "informative",
                Status = PostRestoreSqlCheckStatus.Failed,
                Severity = PostRestoreSqlCheckSeverity.Informative,
                ReasonCode = "HYPOTHETICAL",
                FailureCategory = PostRestoreSqlFailureCategory.Unknown,
                Summary = "informative failure must not block L4"
            }
        };

        Assert.True(PostRestoreDrillSqlChecker.ComputeL4LayerPass(checks));
    }

    [Fact]
    public void ComputeL4LayerPass_false_when_passed_but_failure_category_unknown()
    {
        var checks = new PostRestoreSqlCheckRow[]
        {
            new()
            {
                Id = "r",
                Name = "n",
                Category = "platform",
                Status = PostRestoreSqlCheckStatus.Passed,
                Severity = PostRestoreSqlCheckSeverity.RequiredForL4,
                ReasonCode = PostRestoreSqlReasonCodes.TableAccessible,
                FailureCategory = PostRestoreSqlFailureCategory.Unknown,
                Summary = "inconsistent row must not count as L4 success"
            }
        };

        Assert.False(PostRestoreDrillSqlChecker.ComputeL4LayerPass(checks));
    }
}
