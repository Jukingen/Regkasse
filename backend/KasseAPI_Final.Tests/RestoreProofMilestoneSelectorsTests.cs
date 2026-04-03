using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreProofMilestoneSelectorsTests
{
    [Fact]
    public void IsL4ContinuityProven_requires_executed_and_passed()
    {
        var ok = new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Succeeded,
            PostRestoreContinuityChecksExecuted = true,
            PostRestoreContinuityChecksPassed = true
        };
        Assert.True(RestoreProofMilestoneSelectors.IsL4ContinuityProven(ok));

        var skipped = new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Succeeded,
            PostRestoreContinuityChecksExecuted = false,
            PostRestoreContinuityChecksPassed = null
        };
        Assert.False(RestoreProofMilestoneSelectors.IsL4ContinuityProven(skipped));
    }

    [Fact]
    public void IsL5HttpSmokeProven_requires_executed_and_passed_bool()
    {
        var ok = new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Succeeded,
            ApplicationSmokeProbeExecuted = true,
            ApplicationSmokeProbePassed = true
        };
        Assert.True(RestoreProofMilestoneSelectors.IsL5HttpSmokeProven(ok));

        var inconclusive = new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Succeeded,
            ApplicationSmokeProbeExecuted = true,
            ApplicationSmokeProbePassed = null
        };
        Assert.False(RestoreProofMilestoneSelectors.IsL5HttpSmokeProven(inconclusive));
    }
}
