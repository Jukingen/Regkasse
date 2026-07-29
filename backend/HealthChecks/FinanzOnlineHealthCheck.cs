using KasseAPI_Final.Models;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.HealthChecks;

/// <summary>
/// Reports FinanzOnline simulation vs real SOAP posture.
/// In Production/Staging (when TSE lock applies), simulation is <see cref="HealthStatus.Unhealthy"/>.
/// In Development, simulation is allowed (<see cref="HealthStatus.Healthy"/>).
/// </summary>
public sealed class FinanzOnlineHealthCheck : IHealthCheck
{
    public const string Name = "finanzonline";

    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IOptionsMonitor<FinanzOnlineSessionOptions> _sessionOptions;

    public FinanzOnlineHealthCheck(
        IHostEnvironment environment,
        IConfiguration configuration,
        IOptionsMonitor<TseOptions> tseOptions,
        IOptionsMonitor<FinanzOnlineSessionOptions> sessionOptions)
    {
        _environment = environment;
        _configuration = configuration;
        _tseOptions = tseOptions;
        _sessionOptions = sessionOptions;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var simulated = TseFiscalConfigLockEvaluator.IsFinanzOnlineSimulated(_configuration)
                        || _sessionOptions.CurrentValue.UseSimulation;
        var lockApplies = TseFiscalConfigLockEvaluator.LockAppliesToHost(
            _environment,
            _tseOptions.CurrentValue);

        var data = new Dictionary<string, object>
        {
            ["useSimulation"] = simulated,
            ["lockApplies"] = lockApplies,
            ["hostEnvironment"] = _environment.EnvironmentName,
        };

        if (!simulated)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy("FinanzOnline is configured for real transport.", data));
        }

        if (!lockApplies)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy(
                    "FinanzOnline simulation is allowed in this host environment.",
                    data));
        }

        return Task.FromResult(
            HealthCheckResult.Unhealthy(
                "FinanzOnline simulation is forbidden when the Production fiscal lock applies.",
                data: data));
    }
}
