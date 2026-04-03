using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// L6 rollup → <see cref="RecoveryProofBand"/> ve geriye dönük <see cref="RecoveryProofOutcome"/>.
/// </summary>
public static class ExternalDependencyProofBandMapper
{
    public static RecoveryProofBand ToRecoveryProofBand(ExternalDependencyRecoveryEvidenceBlock block)
    {
        if (block.Rollup != null)
            return ToRecoveryProofBand(block.Rollup);
        return new RecoveryProofBand
        {
            Outcome = block.OverallOutcome,
            Detail = block.Interpretation ?? "external_dependency_recovery_legacy_block"
        };
    }

    public static RecoveryProofBand ToRecoveryProofBand(ExternalDependencyProofRollup rollup)
    {
        var detail = rollup.Summary ?? rollup.Notes ?? "l6_external_dependency";
        return rollup.OverallState switch
        {
            ExternalDependencyProofState.Passed => new RecoveryProofBand
            {
                Outcome = RecoveryProofOutcome.Passed,
                Detail = detail
            },
            ExternalDependencyProofState.Failed => new RecoveryProofBand
            {
                Outcome = RecoveryProofOutcome.Failed,
                Detail = detail
            },
            _ => new RecoveryProofBand
            {
                Outcome = RecoveryProofOutcome.Partial,
                Detail = detail
            }
        };
    }

    /// <summary>Eski <c>OverallOutcome</c> alanı için; çoğu iskelet çalıştırmada <see cref="RecoveryProofOutcome.Partial"/> kalır.</summary>
    public static RecoveryProofOutcome ToLegacyOverallOutcome(ExternalDependencyProofRollup rollup)
    {
        return rollup.OverallState switch
        {
            ExternalDependencyProofState.Passed => RecoveryProofOutcome.Passed,
            ExternalDependencyProofState.Failed => RecoveryProofOutcome.Failed,
            _ => RecoveryProofOutcome.Partial
        };
    }
}
