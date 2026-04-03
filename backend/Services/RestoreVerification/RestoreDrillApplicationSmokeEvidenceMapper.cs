using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// <see cref="RestoreDrillApplicationSmokeResultKind"/> → <see cref="RecoveryProofBand"/> (kanıt şeridi).
/// </summary>
public static class RestoreDrillApplicationSmokeEvidenceMapper
{
    public static RecoveryProofBand ToValidityBand(RestoreDrillApplicationSmokeResultKind kind, string? detail) =>
        kind switch
        {
            RestoreDrillApplicationSmokeResultKind.Passed => new RecoveryProofBand
            {
                Outcome = RecoveryProofOutcome.Passed,
                Detail = detail ?? "passed"
            },
            RestoreDrillApplicationSmokeResultKind.Failed => new RecoveryProofBand
            {
                Outcome = RecoveryProofOutcome.Failed,
                Detail = detail ?? "failed"
            },
            RestoreDrillApplicationSmokeResultKind.NotAttempted => new RecoveryProofBand
            {
                Outcome = RecoveryProofOutcome.NotConfigured,
                Detail = detail ?? "not_attempted"
            },
            RestoreDrillApplicationSmokeResultKind.NotSupported => new RecoveryProofBand
            {
                Outcome = RecoveryProofOutcome.Skipped,
                Detail = detail ?? "not_supported"
            },
            RestoreDrillApplicationSmokeResultKind.Inconclusive => new RecoveryProofBand
            {
                Outcome = RecoveryProofOutcome.Partial,
                Detail = detail ?? "inconclusive"
            },
            _ => new RecoveryProofBand { Outcome = RecoveryProofOutcome.NotConfigured, Detail = detail }
        };
}
