using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreVerificationVerdictEvaluatorTests
{
    [Fact]
    public void Evaluate_Queued_IsPending()
    {
        var run = new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Queued,
            TriggerSource = RestoreVerificationTriggerSource.Manual
        };

        var snap = RestoreVerificationVerdictEvaluator.Evaluate(run);

        Assert.Equal(RestoreVerificationVerdictEvaluator.VerdictPending, snap.Verdict);
        Assert.Empty(snap.FailedCheckIds);
    }

    [Fact]
    public void Evaluate_Succeeded_WithSkippedFiscal_IsPassed()
    {
        var run = new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            PgRestoreListPassed = true,
            RestoreAttemptExecuted = true,
            RestoreAttemptPassed = true,
            PostRestoreContinuityChecksExecuted = true,
            PostRestoreContinuityChecksPassed = true,
            FiscalContinuityLayerPassed = true,
            FiscalSqlSkipped = true,
            FiscalSqlSkipReason = "FISCAL_CONNECTION_NOT_CONFIGURED",
            CompletedAt = DateTime.UtcNow
        };

        var snap = RestoreVerificationVerdictEvaluator.Evaluate(run);

        Assert.Equal(RestoreVerificationVerdictEvaluator.VerdictPassed, snap.Verdict);
        Assert.Equal(RestoreVerificationVerdictEvaluator.ResultPassed, snap.Checks.Single(c => c.Id == "hash").Result);
        Assert.Equal(RestoreVerificationVerdictEvaluator.ResultPassed, snap.Checks.Single(c => c.Id == "schema").Result);
        Assert.Equal(RestoreVerificationVerdictEvaluator.ResultSkipped, snap.Checks.Single(c => c.Id == "fiscal").Result);
        Assert.Equal("SKIPPED: FISCAL_CONNECTION_NOT_CONFIGURED", snap.FiscalSqlResult);
        Assert.DoesNotContain("fiscal", snap.FailedCheckIds);
    }

    [Fact]
    public void Evaluate_DumpInspectionFailed_IsFailed_AndNamesHash()
    {
        var run = new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Failed,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            PgRestoreListPassed = false,
            FailureDetail = "pg_restore rejected dump",
            FiscalSqlSkipped = true
        };

        var snap = RestoreVerificationVerdictEvaluator.Evaluate(run);

        Assert.Equal(RestoreVerificationVerdictEvaluator.VerdictFailed, snap.Verdict);
        Assert.Contains("hash", snap.FailedCheckIds);
        Assert.Equal(RestoreVerificationVerdictEvaluator.ResultFailed, snap.Checks.Single(c => c.Id == "hash").Result);
    }

    [Fact]
    public void Evaluate_FiscalFailed_ShowsResultFail()
    {
        var run = new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Failed,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            PgRestoreListPassed = true,
            RestoreAttemptExecuted = true,
            RestoreAttemptPassed = true,
            PostRestoreContinuityChecksExecuted = true,
            PostRestoreContinuityChecksPassed = true,
            FiscalSqlSkipped = false,
            FiscalSqlPassed = false,
            FiscalSqlFailCount = 2,
            FiscalSqlWarnCount = 1
        };

        var snap = RestoreVerificationVerdictEvaluator.Evaluate(run);

        Assert.Equal(RestoreVerificationVerdictEvaluator.VerdictFailed, snap.Verdict);
        Assert.Contains("fiscal", snap.FailedCheckIds);
        Assert.Equal("RESULT FAIL (fail=2, warn=1)", snap.FiscalSqlResult);
    }
}
