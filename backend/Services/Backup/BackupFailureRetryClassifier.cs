using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Hangi <see cref="BackupRun.FailureCode"/> / durumların otomatik yeniden kuyruğa alınabileceğini sabitler (dar allowlist).
/// </summary>
public static class BackupFailureRetryClassifier
{
    /// <summary>
    /// Yalnızca geçici kabul edilen yürütme hataları ve (isteğe bağlı) doğrulama bütünlük reddi.
    /// </summary>
    public static bool IsEligibleForAutomaticRetrySchedule(
        BackupRunStatus status,
        string? failureCode,
        bool allowVerificationIntegrityRetry)
    {
        if (status == BackupRunStatus.Failed)
        {
            if (string.IsNullOrWhiteSpace(failureCode))
                return false;
            return failureCode.Equals("PG_DUMP_TIMEOUT", StringComparison.Ordinal)
                   || failureCode.Equals("PG_DUMP_FAILED", StringComparison.Ordinal);
        }

        if (status == BackupRunStatus.VerificationFailed)
        {
            if (!allowVerificationIntegrityRetry)
                return false;
            return string.Equals(failureCode, "VERIFICATION_FAILED", StringComparison.Ordinal);
        }

        return false;
    }
}
