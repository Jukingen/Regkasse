using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class RestoreVerificationOperationalReadinessService : IRestoreVerificationOperationalReadiness
{
    private readonly IOptionsMonitor<RestoreVerificationOptions> _options;
    private readonly IHostEnvironment _environment;

    public RestoreVerificationOperationalReadinessService(
        IOptionsMonitor<RestoreVerificationOptions> options,
        IHostEnvironment environment)
    {
        _options = options;
        _environment = environment;
    }

    public RestoreVerificationConfigurationHealthSnapshot GetConfigurationHealth() =>
        RestoreVerificationConfigurationEvaluation.Evaluate(_options.CurrentValue, _environment);
}
