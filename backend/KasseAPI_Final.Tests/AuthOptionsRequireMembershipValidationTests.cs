using KasseAPI_Final.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// P1: Auth:RequireTenantMembershipForLogin must be true outside Development (ValidateOnStart).
/// </summary>
public sealed class AuthOptionsRequireMembershipValidationTests
{
    private static bool Validate(AuthOptions opts, string environmentName)
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        // Mirrors ApplicationHost AddOptions AuthOptions Validate IHostEnvironment predicate.
        return env.Object.IsDevelopment() || opts.RequireTenantMembershipForLogin;
    }

    [Fact]
    public void Production_RequiresMembershipFlagTrue()
    {
        Assert.False(Validate(new AuthOptions { RequireTenantMembershipForLogin = false }, Environments.Production));
        Assert.True(Validate(new AuthOptions { RequireTenantMembershipForLogin = true }, Environments.Production));
    }

    [Fact]
    public void Staging_RequiresMembershipFlagTrue()
    {
        Assert.False(Validate(new AuthOptions { RequireTenantMembershipForLogin = false }, Environments.Staging));
        Assert.True(Validate(new AuthOptions { RequireTenantMembershipForLogin = true }, Environments.Staging));
    }

    [Fact]
    public void Development_AllowsMembershipFlagFalse()
    {
        Assert.True(Validate(new AuthOptions { RequireTenantMembershipForLogin = false }, Environments.Development));
        Assert.True(Validate(new AuthOptions { RequireTenantMembershipForLogin = true }, Environments.Development));
    }
}
