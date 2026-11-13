using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Configuration;

/// <summary>
/// Ensures the DE SIGN host never points at Austrian SIGN AT or the legacy fiskaly API host,
/// and that <c>Mode=Simulation</c> is Development-only. Never logs secrets.
/// </summary>
public sealed class KassenSicherheitHostOptionsValidator : IValidateOptions<KassenSicherheitOptions>
{
    public const string SimulationRejectedReason =
        "KassenSicherheit:Mode=Simulation is allowed only in Development.";

    public const string PilotLiveEnvironmentRejectedReason =
        "KassenSicherheit:PilotMode requires KassenSicherheit:Environment=TEST. LIVE is rejected.";

    public const string PilotLiveHostRejectedReason =
        "KassenSicherheit:PilotMode requires KassenSicherheit:ApiBaseUrl to use the SIGN DE TEST host (kassensichv-middleware.fiskaly.com). The LIVE host is rejected.";

    public static readonly EventId RejectedEventId = new(71023, "KassenSicherheitHostRejected");
    public static readonly EventId AcceptedEventId = new(71024, "KassenSicherheitHostAccepted");

    private readonly ILogger<KassenSicherheitHostOptionsValidator> _logger;
    private readonly IHostEnvironment _environment;

    public KassenSicherheitHostOptionsValidator(
        ILogger<KassenSicherheitHostOptionsValidator> logger,
        IHostEnvironment environment)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public ValidateOptionsResult Validate(string? name, KassenSicherheitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.Equals(options.Mode?.Trim(), KassenSicherheitOptions.ModeSimulation, StringComparison.OrdinalIgnoreCase)
            && !_environment.IsDevelopment())
        {
            _logger.LogCritical(
                RejectedEventId,
                "KassenSicherheit simulation rejected. Environment={Environment} Reason={Reason}",
                _environment.EnvironmentName,
                SimulationRejectedReason);
            return ValidateOptionsResult.Fail(SimulationRejectedReason);
        }

        if (options.PilotMode
            && !string.Equals(options.Environment?.Trim(), KassenSicherheitOptions.EnvironmentTest, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogCritical(
                RejectedEventId,
                "KassenSicherheit pilot rejected. Environment={Environment} Reason={Reason}",
                options.Environment ?? "(unset)",
                PilotLiveEnvironmentRejectedReason);
            return ValidateOptionsResult.Fail(PilotLiveEnvironmentRejectedReason);
        }

        var url = options.ApiBaseUrl ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            if (!options.PilotMode)
                return ValidateOptionsResult.Success;

            _logger.LogCritical(
                RejectedEventId,
                "KassenSicherheit pilot rejected. ApiBaseUrl=(unset) Reason={Reason}",
                PilotLiveHostRejectedReason);
            return ValidateOptionsResult.Fail(PilotLiveHostRejectedReason);
        }

        if (url.Contains("rksv.fiskaly.com", StringComparison.OrdinalIgnoreCase))
        {
            const string reason =
                "KassenSicherheit:ApiBaseUrl must not use the SIGN AT host (rksv.fiskaly.com). " +
                "Use Fiskaly:ApiBaseUrl for Austria.";
            _logger.LogCritical(RejectedEventId, "KassenSicherheit DE host rejected. ApiBaseUrl={ApiBaseUrl} Reason={Reason}", url, reason);
            return ValidateOptionsResult.Fail(reason);
        }

        if (url.Contains("api.fiskaly.com", StringComparison.OrdinalIgnoreCase))
        {
            const string reason =
                "KassenSicherheit:ApiBaseUrl must not use the legacy api.fiskaly.com host. " +
                "SIGN DE uses the kassensichv host.";
            _logger.LogCritical(RejectedEventId, "KassenSicherheit DE host rejected. ApiBaseUrl={ApiBaseUrl} Reason={Reason}", url, reason);
            return ValidateOptionsResult.Fail(reason);
        }

        if (options.PilotMode && !IsSignDeTestHost(url))
        {
            _logger.LogCritical(
                RejectedEventId,
                "KassenSicherheit pilot rejected. ApiBaseUrl={ApiBaseUrl} Reason={Reason}",
                url,
                PilotLiveHostRejectedReason);
            return ValidateOptionsResult.Fail(PilotLiveHostRejectedReason);
        }

        _logger.LogInformation(AcceptedEventId, "KassenSicherheit DE host = {ApiBaseUrl}", url);
        return ValidateOptionsResult.Success;
    }

    private static bool IsSignDeTestHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return string.Equals(uri.Host, KassenSicherheitOptions.SignDeTestHost, StringComparison.OrdinalIgnoreCase);
    }
}
