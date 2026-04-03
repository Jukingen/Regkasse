using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// L4 bileşik kanıt: klon üzerinde süreklilik kapsamı varsa zorunlu; fiscal betik her zaman gerekli (atlanmamış ve geçmiş).
/// </summary>
public static class RestoreDrillFiscalContinuityLayer
{
    /// <summary>
    /// Terminal başarı öncesi alanlardan türetilir; <see cref="RestoreVerificationRun.Status"/> henüz Succeeded olmayabilir.
    /// </summary>
    public static bool ComputeEvidence(RestoreVerificationOptions opts, RestoreVerificationRun run)
    {
        if (run.FiscalSqlSkipped || run.FiscalSqlPassed != true)
            return false;

        var continuityInScope = opts.PostRestoreSqlChecksEnabled
                                  && run.RestoreAttemptExecuted
                                  && run.RestoreAttemptPassed == true;

        if (!continuityInScope)
            return true;

        return run.PostRestoreContinuityChecksExecuted && run.PostRestoreContinuityChecksPassed == true;
    }
}
