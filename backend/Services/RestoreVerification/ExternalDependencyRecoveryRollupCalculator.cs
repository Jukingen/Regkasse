using System.Linq;
using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// L6 alan kanıtlarından konsolide durum; restore başarısı tek başına <see cref="ExternalDependencyProofState.Passed"/> üretmez.
/// </summary>
public static class ExternalDependencyRecoveryRollupCalculator
{
    /// <summary>
    /// Öncelik: <see cref="ExternalDependencyProofState.Failed"/>; ardından eksik otomasyon (<see cref="ExternalDependencyProofState.NotImplemented"/> → genel <see cref="ExternalDependencyProofState.NotProven"/>).
    /// </summary>
    public static ExternalDependencyProofRollup Compute(
        IReadOnlyList<ExternalDependencyDomainEvidence> domains,
        string? rollupNotes)
    {
        var state = ComputeOverallState(domains);
        return new ExternalDependencyProofRollup
        {
            OverallState = state,
            Summary = BuildSummary(state, domains),
            Notes = rollupNotes
        };
    }

    public static ExternalDependencyProofState ComputeOverallState(IReadOnlyList<ExternalDependencyDomainEvidence> domains)
    {
        if (domains == null || domains.Count == 0)
            return ExternalDependencyProofState.NotProven;

        if (domains.Any(d => d.State == ExternalDependencyProofState.Failed))
            return ExternalDependencyProofState.Failed;

        if (domains.Any(d => d.State == ExternalDependencyProofState.NotImplemented))
            return ExternalDependencyProofState.NotProven;

        if (domains.All(d => d.State == ExternalDependencyProofState.Passed))
            return ExternalDependencyProofState.Passed;

        if (domains.Any(d => d.State == ExternalDependencyProofState.ManualCheckRequired))
            return ExternalDependencyProofState.ManualCheckRequired;

        if (domains.Any(d => d.State == ExternalDependencyProofState.NotProven))
            return ExternalDependencyProofState.NotProven;

        return ExternalDependencyProofState.NotProven;
    }

    private static string BuildSummary(ExternalDependencyProofState overall, IReadOnlyList<ExternalDependencyDomainEvidence> domains)
    {
        var domainHint = string.Join(
            ",",
            domains.Select(d => $"{d.Domain}:{d.State}"));
        return $"l6_overall={overall};domains=[{domainHint}]";
    }
}
