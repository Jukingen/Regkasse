using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.OperationalRuns;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupFailureRetryClassifierTests
{
    public static TheoryData<string, string> FailedRetryableCases => new()
    {
        { "PG_DUMP_TIMEOUT", BackupFailureRetryClassifier.ClassifiedReasons.PgDumpTransientTimeout },
        { "PG_DUMP_FAILED", BackupFailureRetryClassifier.ClassifiedReasons.PgDumpTransientExecution },
        { "WORKER_LOST", BackupFailureRetryClassifier.ClassifiedReasons.StaleWorkerLostRunning }
    };

    [Theory]
    [MemberData(nameof(FailedRetryableCases))]
    public void Failed_retryable_codes(string code, string? expectReason)
    {
        Assert.True(BackupFailureRetryClassifier.TryGetEligibleClassification(
            BackupRunStatus.Failed,
            code,
            allowVerificationIntegrityRetry: false,
            out var reason));
        Assert.Equal(expectReason, reason);
    }

    [Theory]
    [InlineData("UNHANDLED_EXCEPTION")]
    [InlineData("EXECUTION_FAILED")]
    [InlineData("   ")]
    public void Failed_non_retryable_codes(string code)
    {
        Assert.False(BackupFailureRetryClassifier.TryGetEligibleClassification(
            BackupRunStatus.Failed,
            code,
            false,
            out _));
    }

    [Fact]
    public void Verification_worker_lost_retryable_without_integrity_flag()
    {
        Assert.True(BackupFailureRetryClassifier.TryGetEligibleClassification(
            BackupRunStatus.VerificationFailed,
            StaleRunRecoveryCodes.VerificationWorkerLost,
            allowVerificationIntegrityRetry: false,
            out var r));
        Assert.Equal(BackupFailureRetryClassifier.ClassifiedReasons.StaleVerificationWorkerLost, r);
    }

    [Fact]
    public void Verification_failed_integrity_only_when_policy_allows()
    {
        Assert.False(BackupFailureRetryClassifier.TryGetEligibleClassification(
            BackupRunStatus.VerificationFailed,
            "VERIFICATION_FAILED",
            allowVerificationIntegrityRetry: false,
            out _));
        Assert.True(BackupFailureRetryClassifier.TryGetEligibleClassification(
            BackupRunStatus.VerificationFailed,
            "VERIFICATION_FAILED",
            allowVerificationIntegrityRetry: true,
            out var r));
        Assert.Equal(BackupFailureRetryClassifier.ClassifiedReasons.VerificationIntegrityOptionalRetry, r);
    }

    [Theory]
    [InlineData("INCOMPLETE_VERIFIED_ARTIFACT_SET")]
    [InlineData("EXTERNAL_ARCHIVE_FAILED")]
    [InlineData("VERIFIER_EXCEPTION")]
    public void Verification_failed_integrity_adjacent_stays_terminal_even_if_policy_on(string code)
    {
        Assert.False(BackupFailureRetryClassifier.TryGetEligibleClassification(
            BackupRunStatus.VerificationFailed,
            code,
            allowVerificationIntegrityRetry: true,
            out _));
    }

    [Fact]
    public void Stale_constants_align_with_reaper_failure_codes()
    {
        Assert.Equal(StaleRunRecoveryCodes.WorkerLost, "WORKER_LOST");
        Assert.Equal(StaleRunRecoveryCodes.VerificationWorkerLost, "VERIFICATION_WORKER_LOST");
    }
}
