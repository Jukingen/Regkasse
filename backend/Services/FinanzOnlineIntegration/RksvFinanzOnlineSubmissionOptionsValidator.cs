using KasseAPI_Final.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Production fail-closed: Fake RKSV FinanzOnline client is forbidden unless explicitly allowed.
/// </summary>
public sealed class RksvFinanzOnlineSubmissionOptionsValidator : IValidateOptions<RksvFinanzOnlineSubmissionClientOptions>
{
    public static readonly EventId RejectedEventId = new(72001, "RksvFinanzOnlineFakeRejectedInProduction");

    private readonly IHostEnvironment _environment;
    private readonly ILogger<RksvFinanzOnlineSubmissionOptionsValidator> _logger;

    public RksvFinanzOnlineSubmissionOptionsValidator(
        IHostEnvironment environment,
        ILogger<RksvFinanzOnlineSubmissionOptionsValidator> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, RksvFinanzOnlineSubmissionClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!_environment.IsProduction())
            return ValidateOptionsResult.Success;

        if (options.ClientKind != RksvFinanzOnlineSubmissionClientKind.Fake)
            return ValidateOptionsResult.Success;

        if (options.AllowFakeClientInProduction)
        {
            _logger.LogCritical(
                RejectedEventId,
                "RKSV FinanzOnline Fake client allowed in Production via AllowFakeClientInProduction. This is not fiscally valid.");
            return ValidateOptionsResult.Success;
        }

        _logger.LogCritical(
            RejectedEventId,
            "RKSV FinanzOnline ClientKind=Fake is forbidden in Production. Set ClientKind=Real or AllowFakeClientInProduction=true (escape hatch).");

        return ValidateOptionsResult.Fail(
            "FinanzOnline:RksvSubmission:ClientKind=Fake is forbidden when ASPNETCORE_ENVIRONMENT=Production.");
    }
}
