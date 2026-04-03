using System.Linq;
using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// L4 süreklilik SQL kanıtı: rollup metin ve <see cref="PostRestoreContinuityProofState"/> eşlemesi (JSON evidence ile uyumlu).
/// </summary>
public static class PostRestoreContinuityEvidenceFormatter
{
    public static PostRestoreContinuityProofState MapProofState(PostRestoreDrillSqlOutcome outcome)
    {
        if (!outcome.Executed)
            return PostRestoreContinuityProofState.NotExecuted;
        if (outcome.Passed)
            return PostRestoreContinuityProofState.Passed;

        var required = outcome.Checks.Where(c => c.Severity == PostRestoreSqlCheckSeverity.RequiredForL4).ToList();
        if (required.Any(c => c.Status == PostRestoreSqlCheckStatus.Failed))
            return PostRestoreContinuityProofState.Failed;
        if (required.Any(c => c.Status == PostRestoreSqlCheckStatus.Inconclusive))
            return PostRestoreContinuityProofState.Inconclusive;

        return PostRestoreContinuityProofState.Failed;
    }

    public static string BuildContinuityChecksSummary(PostRestoreDrillSqlOutcome outcome)
    {
        var checks = outcome.Checks;
        var req = checks.Where(c => c.Severity == PostRestoreSqlCheckSeverity.RequiredForL4).ToList();
        var rp = req.Count(c => c.Status == PostRestoreSqlCheckStatus.Passed);
        var rf = req.Count(c => c.Status == PostRestoreSqlCheckStatus.Failed);
        var ri = req.Count(c => c.Status == PostRestoreSqlCheckStatus.Inconclusive);
        var dom = PostRestoreSqlFailureCategoryMapper.ComputeDominantFailureCategory(checks);
        var domStr = dom?.ToString() ?? "none";
        return
            $"L4 continuity SQL: total={checks.Count}, required_pass={rp}, required_failed={rf}, required_inconclusive={ri}, dominant_category={domStr}, layer_pass={outcome.Passed}";
    }
}
