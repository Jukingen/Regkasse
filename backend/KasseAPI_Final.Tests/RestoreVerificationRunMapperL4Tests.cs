using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.RestoreVerification;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreVerificationRunMapperL4Tests
{
    [Fact]
    public void ToDto_PostRestoreL4_null_when_checks_not_executed()
    {
        var run = new RestoreVerificationRun
        {
            PostRestoreContinuityChecksExecuted = false,
            PostRestoreContinuityChecksPassed = null
        };

        var dto = RestoreVerificationRunMapper.ToDto(run);

        Assert.Null(dto.PostRestoreL4ContinuityProofState);
    }

    [Fact]
    public void ToDto_PostRestoreL4_passed_when_executed_and_passed()
    {
        var run = new RestoreVerificationRun
        {
            PostRestoreContinuityChecksExecuted = true,
            PostRestoreContinuityChecksPassed = true
        };

        var dto = RestoreVerificationRunMapper.ToDto(run);

        Assert.Equal(PostRestoreContinuityProofState.Passed, dto.PostRestoreL4ContinuityProofState);
    }

    [Fact]
    public void ToDto_PostRestoreL4_failed_when_executed_and_not_passed()
    {
        var run = new RestoreVerificationRun
        {
            PostRestoreContinuityChecksExecuted = true,
            PostRestoreContinuityChecksPassed = false
        };

        var dto = RestoreVerificationRunMapper.ToDto(run);

        Assert.Equal(PostRestoreContinuityProofState.Failed, dto.PostRestoreL4ContinuityProofState);
    }

    [Fact]
    public void ToDto_PostRestoreL4_uses_persisted_column_when_set()
    {
        var run = new RestoreVerificationRun
        {
            PostRestoreContinuityChecksExecuted = true,
            PostRestoreContinuityChecksPassed = false,
            PostRestoreL4ContinuityProofState = PostRestoreContinuityProofState.Inconclusive
        };

        var dto = RestoreVerificationRunMapper.ToDto(run);

        Assert.Equal(PostRestoreContinuityProofState.Inconclusive, dto.PostRestoreL4ContinuityProofState);
    }
}
