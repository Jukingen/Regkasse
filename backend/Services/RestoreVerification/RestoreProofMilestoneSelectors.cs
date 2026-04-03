using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Restore drill satırları için muhafazakâr ön koşullar (kanıt abartısını önler).
/// </summary>
public static class RestoreProofMilestoneSelectors
{
    public static bool IsTerminalSucceeded(RestoreVerificationRun r) =>
        r.Status == RestoreVerificationStatus.Succeeded;

    /// <summary>
    /// L4 (süreklilik SQL): L3 başarı + post-restore kontrolleri kesin geçti (inconclusive başarı sayılmaz).
    /// </summary>
    public static bool IsL4ContinuityProven(RestoreVerificationRun r) =>
        r.Status == RestoreVerificationStatus.Succeeded
        && r.PostRestoreContinuityChecksExecuted
        && r.PostRestoreContinuityChecksPassed == true;

    /// <summary>L5b: yapılandırılmış HTTP duman testi kesin geçti (opsiyonel katman).</summary>
    public static bool IsL5HttpSmokeProven(RestoreVerificationRun r) =>
        r.Status == RestoreVerificationStatus.Succeeded
        && r.ApplicationSmokeProbeExecuted
        && r.ApplicationSmokeProbePassed == true;

    /// <summary>L5 (uygulama dumanı): L4 sonrası izole geri yüklenen DB üzerinde duman kesin geçti.</summary>
    public static bool IsL5aRestoredDbSmokeProven(RestoreVerificationRun r) =>
        r.Status == RestoreVerificationStatus.Succeeded
        && r.RestoredDatabaseApplicationSmokeExecuted
        && r.RestoredDatabaseApplicationSmokePassed == true;

    public static DateTime MilestoneAsOfUtc(RestoreVerificationRun r) =>
        r.CompletedAt ?? r.StartedAt ?? r.RequestedAt;
}
