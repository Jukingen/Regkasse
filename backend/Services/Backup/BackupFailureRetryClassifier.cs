using System.Diagnostics.CodeAnalysis;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.OperationalRuns;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Otomatik yeniden kuyruğa alma için dar allowlist: yalnızca geçici/altyapı sınıfları.
/// Bütünlük / eksik artefakt / harici arşiv hataları varsayılan olarak terminal kalır.
/// </summary>
/// <remarks>
/// Sınıflandırma özeti (FailureCode → anlam):
/// <list type="bullet">
/// <item><description><b>PG_DUMP_TIMEOUT</b>, <b>PG_DUMP_FAILED</b> (Failed): pg_dump geçici hata — retryable.</description></item>
/// <item><description><b>WORKER_LOST</b> (Failed): stale reaper, Running sırasında lease kaybı — retryable.</description></item>
/// <item><description><b>VERIFICATION_WORKER_LOST</b> (VerificationFailed): stale reaper, doğrulama aşamasında worker kaybı — retryable (bütünlük değil).</description></item>
/// <item><description><b>VERIFICATION_FAILED</b> (VerificationFailed): yalnızca <c>AllowAutomaticRetryAfterVerificationIntegrityFailure=true</c> iken retryable.</description></item>
/// </list>
/// <b>Retryable değil</b> (örnek): <c>INCOMPLETE_VERIFIED_ARTIFACT_SET</c>, <c>EXTERNAL_ARCHIVE_FAILED</c>, <c>VERIFIER_EXCEPTION</c>,
/// <c>UNHANDLED_EXCEPTION</c>, <c>EXECUTION_FAILED</c>, <c>CANCELLED</c>, <c>PG_DUMP_CANCELLED</c>.
/// </remarks>
public static class BackupFailureRetryClassifier
{
    /// <summary>Sabit neden kodları — API ve loglarda stabil.</summary>
    public static class ClassifiedReasons
    {
        public const string PgDumpTransientTimeout = "PG_DUMP_TRANSIENT_TIMEOUT";

        public const string PgDumpTransientExecution = "PG_DUMP_TRANSIENT_EXECUTION";

        public const string StaleWorkerLostRunning = "STALE_WORKER_LOST_RUNNING";

        public const string StaleVerificationWorkerLost = "STALE_VERIFICATION_WORKER_LOST";

        public const string VerificationIntegrityOptionalRetry = "VERIFICATION_INTEGRITY_OPTIONAL_RETRY";
    }

    /// <summary>
    /// Gecikme: <c>min(24h, max(5s, AutomaticRetryInitialDelay) × 2^min(AutomaticRetryCount, 10))</c> — <see cref="BackupAutomaticRetryCoordinator.ComputeDeterministicRetryDelay"/>.
    /// </summary>
    public static bool IsEligibleForAutomaticRetrySchedule(
        BackupRunStatus status,
        string? failureCode,
        bool allowVerificationIntegrityRetry) =>
        TryGetEligibleClassification(status, failureCode, allowVerificationIntegrityRetry, out _);

    /// <summary>
    /// Uygunluk + sınıflandırılmış neden (persist / DTO / log).
    /// </summary>
    public static bool TryGetEligibleClassification(
        BackupRunStatus status,
        string? failureCode,
        bool allowVerificationIntegrityRetry,
        [NotNullWhen(true)] out string? classifiedReason)
    {
        classifiedReason = null;
        if (string.IsNullOrWhiteSpace(failureCode))
            return false;

        var code = failureCode.Trim();

        if (status == BackupRunStatus.Failed)
        {
            if (code.Equals("PG_DUMP_TIMEOUT", StringComparison.Ordinal))
            {
                classifiedReason = ClassifiedReasons.PgDumpTransientTimeout;
                return true;
            }

            if (code.Equals("PG_DUMP_FAILED", StringComparison.Ordinal))
            {
                classifiedReason = ClassifiedReasons.PgDumpTransientExecution;
                return true;
            }

            if (code.Equals(StaleRunRecoveryCodes.WorkerLost, StringComparison.Ordinal))
            {
                classifiedReason = ClassifiedReasons.StaleWorkerLostRunning;
                return true;
            }

            return false;
        }

        if (status == BackupRunStatus.VerificationFailed)
        {
            if (code.Equals(StaleRunRecoveryCodes.VerificationWorkerLost, StringComparison.Ordinal))
            {
                classifiedReason = ClassifiedReasons.StaleVerificationWorkerLost;
                return true;
            }

            if (allowVerificationIntegrityRetry
                && string.Equals(code, "VERIFICATION_FAILED", StringComparison.Ordinal))
            {
                classifiedReason = ClassifiedReasons.VerificationIntegrityOptionalRetry;
                return true;
            }

            return false;
        }

        return false;
    }
}
