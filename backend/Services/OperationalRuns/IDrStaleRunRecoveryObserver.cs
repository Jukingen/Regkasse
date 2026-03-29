namespace KasseAPI_Final.Services.OperationalRuns;

/// <summary>
/// Stale reaper bir satırı terminal yaptıktan sonra uyarı ve metrik tetikler.
/// </summary>
public interface IDrStaleRunRecoveryObserver
{
    /// <param name="phase"><c>running</c> veya <c>awaiting_verification</c> (restore için <c>running</c>).</param>
    void OnStaleBackupRunRecovered(Guid runId, string phase);

    void OnStaleRestoreVerificationRunRecovered(Guid runId);
}
