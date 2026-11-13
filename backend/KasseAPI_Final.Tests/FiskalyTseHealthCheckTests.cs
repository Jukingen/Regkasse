using KasseAPI_Final.DTOs;
using KasseAPI_Final.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyTseHealthCheckTests
{
    [Fact]
    public void Disabled_IsHealthy()
    {
        var result = FiskalyTseHealthCheck.FromStatus(new FiskalyStatusDto { IsEnabled = false });
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void EnabledWithoutCredentials_IsDegraded()
    {
        var result = FiskalyTseHealthCheck.FromStatus(new FiskalyStatusDto
        {
            IsEnabled = true,
            IsConfigured = false,
        });
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public void AuthenticationFailure_IsUnhealthy()
    {
        var result = FiskalyTseHealthCheck.FromStatus(new FiskalyStatusDto
        {
            IsEnabled = true,
            IsConfigured = true,
            IsAuthenticated = false,
            Error = "Authentication did not succeed.",
        });
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public void Authenticated_IsHealthy()
    {
        var result = FiskalyTseHealthCheck.FromStatus(new FiskalyStatusDto
        {
            IsEnabled = true,
            IsConfigured = true,
            IsAuthenticated = true,
            Environment = "TEST",
        });
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
