using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.HealthChecks;

/// <summary>
/// Reports whether FinanzOnline session transport is configured for real SOAP (not simulation).
/// </summary>
public sealed class FinanzOnlineHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<FinanzOnlineSessionOptions> _sessionOptions;

    public FinanzOnlineHealthCheck(IOptionsMonitor<FinanzOnlineSessionOptions> sessionOptions)
    {
        _sessionOptions = sessionOptions;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_sessionOptions.CurrentValue.UseSimulation)
        {
            return Task.FromResult(
                HealthCheckResult.Degraded("FinanzOnline is in simulation mode"));
        }

        return Task.FromResult(
            HealthCheckResult.Healthy("FinanzOnline is configured for production"));
    }
}
