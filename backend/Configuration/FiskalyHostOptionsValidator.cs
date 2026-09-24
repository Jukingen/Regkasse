using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Configuration;

/// <summary>
/// Ensures Austrian SIGN AT (<c>Fiskaly:</c>) never points at the SIGN DE host.
/// Never logs secrets (ApiKey / ApiSecret / certificates).
/// </summary>
public sealed class FiskalyHostOptionsValidator : IValidateOptions<FiskalyOptions>
{
    public static readonly EventId RejectedEventId = new(71021, "FiskalyHostRejected");
    public static readonly EventId LegacyHostEventId = new(71022, "FiskalyLegacyHost");

    private readonly ILogger<FiskalyHostOptionsValidator> _logger;

    public FiskalyHostOptionsValidator(ILogger<FiskalyHostOptionsValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ValidateOptionsResult Validate(string? name, FiskalyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var url = options.BaseUrl ?? string.Empty;

        if (url.Contains("kassensichv", StringComparison.OrdinalIgnoreCase))
        {
            const string reason =
                "Fiskaly:ApiBaseUrl/BaseUrl must not use the SIGN DE host (kassensichv). " +
                "Austrian SIGN AT requires https://rksv.fiskaly.com/api/v1. " +
                "Use KassenSicherheit:ApiBaseUrl for DE.";
            _logger.LogCritical(
                RejectedEventId,
                "Fiskaly AT host rejected. BaseUrl={BaseUrl} Reason={Reason}",
                url,
                reason);
            return ValidateOptionsResult.Fail(reason);
        }

        if (url.Contains("api.fiskaly.com", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("rksv.fiskaly.com", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                LegacyHostEventId,
                "Fiskaly AT host uses legacy api.fiskaly.com ({BaseUrl}). Prefer https://rksv.fiskaly.com/api/v1.",
                url);
        }

        return ValidateOptionsResult.Success;
    }
}
