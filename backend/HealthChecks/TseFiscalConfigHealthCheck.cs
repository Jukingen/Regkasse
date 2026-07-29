using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.HealthChecks;

/// <summary>
/// Reports Production/Staging TSE fiscal configuration posture (not device probe health).
/// Mapped at <c>/health/tse/mode</c>. Does not include secrets in data.
/// </summary>
public sealed class TseFiscalConfigHealthCheck : IHealthCheck
{
    public const string Name = "tse-fiscal-config";

    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;

    public TseFiscalConfigHealthCheck(
        IHostEnvironment environment,
        IConfiguration configuration,
        IOptionsMonitor<TseOptions> tseOptions)
    {
        _environment = environment;
        _configuration = configuration;
        _tseOptions = tseOptions;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = _tseOptions.CurrentValue;
        var eval = TseFiscalConfigLockEvaluator.Evaluate(_environment, _configuration, options);

        var data = new Dictionary<string, object>
        {
            ["lockApplies"] = eval.LockApplies,
            ["isSafe"] = eval.IsSafe,
            ["escapeHatchActive"] = eval.EscapeHatchActive,
            ["tseMode"] = options.TseMode ?? string.Empty,
            ["mode"] = options.Mode ?? string.Empty,
            ["provider"] = string.IsNullOrWhiteSpace(options.Provider) ? "(unset)" : options.Provider.Trim(),
            ["hostEnvironment"] = _environment.EnvironmentName,
        };
        if (eval.Reasons.Count > 0)
            data["reasons"] = string.Join("; ", eval.Reasons);

        if (!eval.LockApplies)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy("TSE fiscal config lock does not apply to this host.", data));
        }

        if (eval.IsSafe)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy("TSE fiscal configuration is Production-safe.", data));
        }

        if (eval.EscapeHatchActive)
        {
            return Task.FromResult(
                HealthCheckResult.Degraded(
                    "TSE fiscal configuration is unsafe but AllowUnsafeFiscalModesInProduction is enabled.",
                    data: data));
        }

        return Task.FromResult(
            HealthCheckResult.Unhealthy(
                "TSE fiscal configuration is unsafe for Production/Staging.",
                data: data));
    }
}
