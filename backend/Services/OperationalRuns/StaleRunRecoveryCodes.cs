namespace KasseAPI_Final.Services.OperationalRuns;

/// <summary>Lease süresi dolduğunda reaper tarafından yazılan failure_code değerleri (İngilizce sabitler).</summary>
public static class StaleRunRecoveryCodes
{
    public const string WorkerLost = "WORKER_LOST";

    public const string VerificationWorkerLost = "VERIFICATION_WORKER_LOST";

    public const string StaleRecoveryReasonRunning =
        "Lease expired while status was Running; worker heartbeat stopped (process crash or hang suspected).";

    public const string StaleRecoveryReasonAwaitingVerification =
        "Lease expired while status was AwaitingVerification; verification worker heartbeat stopped (process crash or hang suspected).";

    public const string StaleRecoveryReasonRestoreRunning =
        "Lease expired while restore verification drill was Running; worker heartbeat stopped (process crash or hang suspected).";

    public const string StaleRecoveryReasonNullLeaseRunning =
        "Backup run stayed Running with no lease_expires_at_utc (legacy row or heartbeat never persisted); exceeded grace window based on RunLeaseTimeout.";

    public const string StaleRecoveryReasonNullLeaseAwaitingVerification =
        "Backup run stayed AwaitingVerification with no lease_expires_at_utc; exceeded grace window based on RunLeaseTimeout.";

    public const string StaleRecoveryReasonNullLeaseRestoreRunning =
        "Restore verification run stayed Running with no lease_expires_at_utc; exceeded grace window based on RunLeaseTimeout.";
}
