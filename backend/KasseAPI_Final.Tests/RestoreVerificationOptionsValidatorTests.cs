using KasseAPI_Final.Configuration;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreVerificationOptionsValidatorTests
{
    private static readonly RestoreVerificationOptionsValidator Validator = new();

    [Fact]
    public void Validate_fails_when_isolated_enabled_without_admin_connection_name()
    {
        var r = Validator.Validate(
            null,
            new RestoreVerificationOptions { IsolatedPgRestoreEnabled = true, IsolatedRestoreAdminConnectionStringName = null });
        Assert.True(r.Failed);
    }

    [Fact]
    public void Validate_succeeds_when_isolated_disabled_without_admin_connection_name()
    {
        var r = Validator.Validate(
            null,
            new RestoreVerificationOptions { IsolatedPgRestoreEnabled = false });
        Assert.False(r.Failed);
    }

    [Fact]
    public void Validate_fails_when_timeout_between_1_and_59()
    {
        var r = Validator.Validate(
            null,
            new RestoreVerificationOptions
            {
                IsolatedPgRestoreEnabled = false,
                IsolatedPgRestoreTimeoutSeconds = 30
            });
        Assert.True(r.Failed);
    }

    [Fact]
    public void Validate_succeeds_when_timeout_zero_uses_runtime_default()
    {
        var r = Validator.Validate(
            null,
            new RestoreVerificationOptions
            {
                IsolatedPgRestoreEnabled = false,
                IsolatedPgRestoreTimeoutSeconds = 0
            });
        Assert.False(r.Failed);
    }
}
