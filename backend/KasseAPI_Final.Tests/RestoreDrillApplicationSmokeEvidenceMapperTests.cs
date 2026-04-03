using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreDrillApplicationSmokeEvidenceMapperTests
{
    [Theory]
    [InlineData(RestoreDrillApplicationSmokeResultKind.Passed, RecoveryProofOutcome.Passed)]
    [InlineData(RestoreDrillApplicationSmokeResultKind.Failed, RecoveryProofOutcome.Failed)]
    [InlineData(RestoreDrillApplicationSmokeResultKind.NotAttempted, RecoveryProofOutcome.NotConfigured)]
    [InlineData(RestoreDrillApplicationSmokeResultKind.NotSupported, RecoveryProofOutcome.Skipped)]
    [InlineData(RestoreDrillApplicationSmokeResultKind.Inconclusive, RecoveryProofOutcome.Partial)]
    public void ToValidityBand_maps_kind_to_recovery_outcome(
        RestoreDrillApplicationSmokeResultKind kind,
        RecoveryProofOutcome expectedOutcome)
    {
        var band = RestoreDrillApplicationSmokeEvidenceMapper.ToValidityBand(kind, "x");
        Assert.Equal(expectedOutcome, band.Outcome);
        Assert.Equal("x", band.Detail);
    }
}
