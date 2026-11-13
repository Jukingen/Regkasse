using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// CreateBuilder loads user secrets only in Development. Staging local runs must opt in.
/// </summary>
public sealed class HostUserSecretsEnvironmentTests
{
    private static IHostEnvironment Env(string name)
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(name);
        return env.Object;
    }

    [Fact]
    public void Staging_AddsUserSecretsAfterCreateBuilder()
    {
        Assert.True(ApplicationHost.ShouldAddUserSecretsAfterDefaultHostConfig(Env(Environments.Staging)));
    }

    [Fact]
    public void Development_DoesNotAddUserSecretsAgain()
    {
        Assert.False(ApplicationHost.ShouldAddUserSecretsAfterDefaultHostConfig(Env(Environments.Development)));
    }

    [Fact]
    public void Production_DoesNotLoadUserSecrets()
    {
        Assert.False(ApplicationHost.ShouldAddUserSecretsAfterDefaultHostConfig(Env(Environments.Production)));
    }
}
