using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Configuration;

/// <summary>
/// Ensures the DE SIGN host never points at Austrian SIGN AT or the legacy fiskaly API host.
/// Never logs secrets.
/// </summary>
public sealed class KassenSicherheitHostOptionsValidator : IValidateOptions<KassenSicherheitOptions>
{
    public static readonly EventId RejectedEventId = new(71023, "KassenSicherheitHostRejected");
    public static readonly EventId AcceptedEventId = new(71024, "KassenSicherheitHostAccepted");

    private readonly ILogger<KassenSicherheitHostOptionsValidator> _logger;

    public KassenSicherheitHostOptionsValidator(ILogger<KassenSicherheitHostOptionsValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ValidateOptionsResult Validate(string? name, KassenSicherheitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var url = options.ApiBaseUrl ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
            return ValidateOptionsResult.Success;

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

        _logger.LogInformation(AcceptedEventId, "KassenSicherheit DE host = {ApiBaseUrl}", url);
        return ValidateOptionsResult.Success;
    }
}
