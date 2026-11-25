using KasseAPI_Final.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Startup fail-closed validation for <c>FinanzOnline:RksvSubmission</c> outside Development
/// (Production, Staging and any other non-Development environment).
/// <para>
/// Outside Development the RKSV Startbeleg/Jahresbeleg submission client must be a complete, outbound-enabled
/// <see cref="RksvFinanzOnlineSubmissionClientKind.Real"/> configuration. Otherwise every submission would end as
/// a <c>PermanentFailure</c> in the FinanzOnline outbox and the legal Belegcheck window could be missed silently.
/// </para>
/// <para>
/// Escape hatch: <c>ClientKind=Fake</c> together with <c>AllowFakeClientInProduction=true</c> is accepted and logged as
/// Critical. It does not apply to <c>NotImplemented</c>, and it is ignored for <c>Real</c> (all Real checks still apply).
/// </para>
/// Development is never validated here, so the default Fake client keeps working locally.
/// </summary>
public sealed class RksvFinanzOnlineSubmissionOptionsValidator : IValidateOptions<RksvFinanzOnlineSubmissionClientOptions>
{
    /// <summary>Fake client is forbidden / escape hatch used.</summary>
    public static readonly EventId RejectedEventId = new(72001, "RksvFinanzOnlineFakeRejectedInProduction");

    /// <summary>Non-Development configuration is not a complete Real client configuration.</summary>
    public static readonly EventId IncompleteConfigEventId = new(72002, "RksvFinanzOnlineSubmissionConfigIncomplete");

    private const string Section = RksvFinanzOnlineSubmissionClientOptions.SectionName;

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

        if (_environment.IsDevelopment())
            return ValidateOptionsResult.Success;

        // Escape hatch: Fake only, never NotImplemented, ignored for Real.
        if (options.ClientKind == RksvFinanzOnlineSubmissionClientKind.Fake && options.AllowFakeClientInProduction)
        {
            _logger.LogCritical(
                RejectedEventId,
                "RKSV FinanzOnline Fake client allowed in {Environment} via AllowFakeClientInProduction. This is not fiscally valid.",
                _environment.EnvironmentName);
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        switch (options.ClientKind)
        {
            case RksvFinanzOnlineSubmissionClientKind.Real:
                break;
            case RksvFinanzOnlineSubmissionClientKind.Fake:
                failures.Add(
                    $"{Section}:ClientKind=Fake is forbidden when ASPNETCORE_ENVIRONMENT={_environment.EnvironmentName} (set ClientKind=Real, or AllowFakeClientInProduction=true as a deliberate escape hatch).");
                break;
            default:
                failures.Add(
                    $"{Section}:ClientKind={options.ClientKind} is forbidden when ASPNETCORE_ENVIRONMENT={_environment.EnvironmentName} (set ClientKind=Real; the escape hatch applies to Fake only).");
                break;
        }

        if (!options.Enabled)
            failures.Add($"{Section}:Enabled must be true.");

        if (!options.AllowOutboundNetworkCalls)
            failures.Add($"{Section}:AllowOutboundNetworkCalls must be true.");

        if (string.IsNullOrWhiteSpace(options.EndpointUrl))
        {
            failures.Add($"{Section}:EndpointUrl is required.");
        }
        else if (!Uri.TryCreate(options.EndpointUrl.Trim(), UriKind.Absolute, out var endpoint)
                 || !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{Section}:EndpointUrl must be an absolute HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(options.ParticipantCredentialsConfigurationKey))
            failures.Add($"{Section}:ParticipantCredentialsConfigurationKey is required (reference name only, never a secret).");

        if (options.TimeoutSeconds < RksvFinanzOnlineSubmissionClientOptions.MinTimeoutSeconds
            || options.TimeoutSeconds > RksvFinanzOnlineSubmissionClientOptions.MaxTimeoutSeconds)
        {
            failures.Add(
                $"{Section}:TimeoutSeconds must be between {RksvFinanzOnlineSubmissionClientOptions.MinTimeoutSeconds} and {RksvFinanzOnlineSubmissionClientOptions.MaxTimeoutSeconds}.");
        }

        if (failures.Count == 0)
            return ValidateOptionsResult.Success;

        var message = string.Join(" ", failures);
        _logger.LogCritical(
            options.ClientKind == RksvFinanzOnlineSubmissionClientKind.Fake ? RejectedEventId : IncompleteConfigEventId,
            "RKSV FinanzOnline submission configuration rejected at startup ({Environment}): {Failures}",
            _environment.EnvironmentName,
            message);

        return ValidateOptionsResult.Fail(failures);
    }
}
