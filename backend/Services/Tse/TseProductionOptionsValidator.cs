using KasseAPI_Final.Models;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Fails startup (<c>ValidateOnStart</c>) when Production/Staging fiscal TSE config is unsafe
/// unless <see cref="TseOptions.AllowUnsafeFiscalModesInProduction"/> is set.
/// Never logs secrets (ApiKey / ApiSecret / PEM).
/// </summary>
public sealed class TseProductionOptionsValidator : IValidateOptions<TseOptions>
{
    public static readonly EventId RejectedEventId = new(71001, "TseProductionConfigRejected");
    public static readonly EventId EscapeHatchEventId = new(71002, "TseUnsafeProductionModeEnabled");

    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TseProductionOptionsValidator> _logger;

    public TseProductionOptionsValidator(
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<TseProductionOptionsValidator> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, TseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = TseFiscalConfigLockEvaluator.Evaluate(_environment, _configuration, options);
        if (!result.LockApplies || result.IsSafe)
            return ValidateOptionsResult.Success;

        if (result.EscapeHatchActive)
        {
            _logger.LogCritical(
                EscapeHatchEventId,
                "TSE unsafe production mode enabled via AllowUnsafeFiscalModesInProduction. TseMode={TseMode} Mode={Mode} Provider={Provider} RksvMode={RksvMode} RksvTseMode={RksvTseMode} Reasons={Reasons}",
                options.TseMode,
                options.Mode,
                string.IsNullOrWhiteSpace(options.Provider) ? "(unset)" : options.Provider.Trim(),
                _configuration["RKSV:Mode"] ?? "(unset)",
                _configuration["RKSV:TseMode"] ?? "(unset)",
                string.Join("; ", result.Reasons));
            return ValidateOptionsResult.Success;
        }

        _logger.LogCritical(
            RejectedEventId,
            "TSE production lock rejected TseMode={TseMode} Mode={Mode} Provider={Provider} RksvMode={RksvMode} RksvTseMode={RksvTseMode} Reasons={Reasons}",
            options.TseMode,
            options.Mode,
            string.IsNullOrWhiteSpace(options.Provider) ? "(unset)" : options.Provider.Trim(),
            _configuration["RKSV:Mode"] ?? "(unset)",
            _configuration["RKSV:TseMode"] ?? "(unset)",
            string.Join("; ", result.Reasons));

        return ValidateOptionsResult.Fail(string.Join(" ", result.Reasons));
    }
}
