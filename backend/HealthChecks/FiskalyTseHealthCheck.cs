using KasseAPI_Final.DTOs;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KasseAPI_Final.HealthChecks;

/// <summary>
/// Probes the Fiskaly SIGN AT connection (authenticate, no secrets in the result).
/// Not part of readiness: a Fiskaly outage must not pull the API out of rotation.
/// Mapped at <c>GET /health/fiskaly</c>. Disabled integrations stay Healthy.
/// </summary>
public sealed class FiskalyTseHealthCheck : IHealthCheck
{
    public const string Name = "fiskaly-tse";

    private readonly IServiceScopeFactory _scopeFactory;

    public FiskalyTseHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IFiskalySettingsService>();
        var status = await settings
            .GetStatusAsync(probeAuthentication: true, cancellationToken)
            .ConfigureAwait(false);
        return FromStatus(status);
    }

    internal static HealthCheckResult FromStatus(FiskalyStatusDto status)
    {
        var data = new Dictionary<string, object>
        {
            ["enabled"] = status.IsEnabled,
            ["configured"] = status.IsConfigured,
            ["authenticated"] = status.IsAuthenticated,
            ["environment"] = status.Environment,
        };
        if (!string.IsNullOrWhiteSpace(status.ScuState))
            data["scuState"] = status.ScuState!;
        if (!string.IsNullOrWhiteSpace(status.Error))
            data["error"] = status.Error!;

        if (!status.IsEnabled)
            return HealthCheckResult.Healthy("Fiskaly TSE is disabled.", data);

        if (!status.IsConfigured)
            return HealthCheckResult.Degraded("Fiskaly TSE is enabled but API credentials are missing.", data: data);

        if (!status.IsAuthenticated)
            return HealthCheckResult.Unhealthy("Fiskaly TSE authentication failed.", data: data);

        return HealthCheckResult.Healthy("Fiskaly TSE connection succeeded.", data);
    }
}
