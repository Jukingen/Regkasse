using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreDrillFiscalContinuityLayerTests
{
    [Fact]
    public void ComputeEvidence_false_when_fiscal_skipped()
    {
        var opts = new RestoreVerificationOptions { PostRestoreSqlChecksEnabled = true };
        var run = new RestoreVerificationRun { FiscalSqlSkipped = true, RestoreAttemptExecuted = true, RestoreAttemptPassed = true };
        Assert.False(RestoreDrillFiscalContinuityLayer.ComputeEvidence(opts, run));
    }

    [Fact]
    public void ComputeEvidence_true_when_continuity_out_of_scope_and_fiscal_passed()
    {
        var opts = new RestoreVerificationOptions { PostRestoreSqlChecksEnabled = false };
        var run = new RestoreVerificationRun
        {
            FiscalSqlSkipped = false,
            FiscalSqlPassed = true,
            RestoreAttemptExecuted = false
        };
        Assert.True(RestoreDrillFiscalContinuityLayer.ComputeEvidence(opts, run));
    }

    [Fact]
    public void ComputeEvidence_requires_post_restore_when_in_scope()
    {
        var opts = new RestoreVerificationOptions { PostRestoreSqlChecksEnabled = true };
        var run = new RestoreVerificationRun
        {
            FiscalSqlSkipped = false,
            FiscalSqlPassed = true,
            RestoreAttemptExecuted = true,
            RestoreAttemptPassed = true,
            PostRestoreContinuityChecksExecuted = true,
            PostRestoreContinuityChecksPassed = false
        };
        Assert.False(RestoreDrillFiscalContinuityLayer.ComputeEvidence(opts, run));
    }
}
