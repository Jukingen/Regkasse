using KasseAPI_Final.Configuration;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RunLeaseOptionsValidationTests
{
    [Fact]
    public void Validate_rejects_heartbeat_not_shorter_than_lease()
    {
        var err = RunLeaseOptionsValidation.Validate(
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(1));
        Assert.NotNull(err);
        Assert.Contains("HeartbeatInterval", err, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_accepts_defaults()
    {
        var err = RunLeaseOptionsValidation.Validate(
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
        Assert.Null(err);
    }

    [Fact]
    public void ValidateStaleRecoveryMultiplier_accepts_two()
    {
        Assert.Null(RunLeaseOptionsValidation.ValidateStaleRecoveryNullLeaseGraceMultiplier(2.0));
    }

    [Fact]
    public void ValidateStaleRecoveryMultiplier_rejects_below_one()
    {
        var err = RunLeaseOptionsValidation.ValidateStaleRecoveryNullLeaseGraceMultiplier(0.5);
        Assert.NotNull(err);
        Assert.Contains("StaleRecoveryNullLeaseGraceMultiplier", err, StringComparison.Ordinal);
    }
}
