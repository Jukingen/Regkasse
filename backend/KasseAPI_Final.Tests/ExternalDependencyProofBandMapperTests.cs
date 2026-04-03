using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ExternalDependencyProofBandMapperTests
{
    [Fact]
    public void ToLegacyOverallOutcome_maps_passed_failed_partial()
    {
        var passed = new ExternalDependencyProofRollup { OverallState = ExternalDependencyProofState.Passed };
        Assert.Equal(RecoveryProofOutcome.Passed, ExternalDependencyProofBandMapper.ToLegacyOverallOutcome(passed));

        var failed = new ExternalDependencyProofRollup { OverallState = ExternalDependencyProofState.Failed };
        Assert.Equal(RecoveryProofOutcome.Failed, ExternalDependencyProofBandMapper.ToLegacyOverallOutcome(failed));

        var np = new ExternalDependencyProofRollup { OverallState = ExternalDependencyProofState.NotProven };
        Assert.Equal(RecoveryProofOutcome.Partial, ExternalDependencyProofBandMapper.ToLegacyOverallOutcome(np));
    }

    [Fact]
    public void ToRecoveryProofBand_partial_for_not_proven()
    {
        var r = new ExternalDependencyProofRollup
        {
            OverallState = ExternalDependencyProofState.NotProven,
            Summary = "x"
        };
        var band = ExternalDependencyProofBandMapper.ToRecoveryProofBand(r);
        Assert.Equal(RecoveryProofOutcome.Partial, band.Outcome);
        Assert.Equal("x", band.Detail);
    }
}
