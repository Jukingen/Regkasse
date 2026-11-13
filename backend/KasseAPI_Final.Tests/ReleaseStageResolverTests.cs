using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Deployment;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ReleaseStageResolverTests
{
    private static IHostEnvironment Host(string name)
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(name);
        return env.Object;
    }

    [Theory]
    [InlineData("Development", "dev")]
    [InlineData("Staging", "staging")]
    [InlineData("Production", "production")]
    public void Resolve_DerivesFromHost_WhenUnconfigured(string host, string expected)
    {
        var previous = Environment.GetEnvironmentVariable("RELEASE_STAGE");
        try
        {
            Environment.SetEnvironmentVariable("RELEASE_STAGE", null);
            var stage = ReleaseStageResolver.Resolve(Host(host), new DeploymentOptions());
            Assert.Equal(expected, stage);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RELEASE_STAGE", previous);
        }
    }

    [Fact]
    public void Resolve_UsesConfiguredReleaseStage()
    {
        var previous = Environment.GetEnvironmentVariable("RELEASE_STAGE");
        try
        {
            Environment.SetEnvironmentVariable("RELEASE_STAGE", null);
            var stage = ReleaseStageResolver.Resolve(
                Host(Environments.Production),
                new DeploymentOptions { ReleaseStage = "staging" });
            Assert.Equal(ReleaseStageResolver.Staging, stage);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RELEASE_STAGE", previous);
        }
    }

    [Fact]
    public void Resolve_CanaryTenantOnProduction_ReturnsCanary()
    {
        var previous = Environment.GetEnvironmentVariable("RELEASE_STAGE");
        try
        {
            Environment.SetEnvironmentVariable("RELEASE_STAGE", null);
            var tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var stage = ReleaseStageResolver.Resolve(
                Host(Environments.Production),
                new DeploymentOptions
                {
                    ReleaseStage = "production",
                    CanaryTenantIds = [tenantId],
                },
                tenantId);
            Assert.Equal(ReleaseStageResolver.Canary, stage);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RELEASE_STAGE", previous);
        }
    }

    [Fact]
    public void Resolve_CanarySlug_MatchesCaseInsensitive()
    {
        var previous = Environment.GetEnvironmentVariable("RELEASE_STAGE");
        try
        {
            Environment.SetEnvironmentVariable("RELEASE_STAGE", null);
            var stage = ReleaseStageResolver.Resolve(
                Host(Environments.Production),
                new DeploymentOptions
                {
                    ReleaseStage = "production",
                    CanaryTenantSlugs = ["pilot-shop"],
                },
                tenantSlug: "Pilot-Shop");
            Assert.Equal(ReleaseStageResolver.Canary, stage);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RELEASE_STAGE", previous);
        }
    }
}
