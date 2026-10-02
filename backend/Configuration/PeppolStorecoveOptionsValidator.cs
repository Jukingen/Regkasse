using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Configuration;

/// <summary>
/// Blocks <c>Peppol:Storecove:Environment=LIVE</c> while the reserved-exit switch is on.
/// LIVE stays closed until package 22-b-5 and a separate ops decision. Does not log secrets.
/// </summary>
public sealed class PeppolStorecoveOptionsValidator : IValidateOptions<PeppolOptions>
{
    public const string LiveWithReservedExitReason =
        "Peppol:Storecove:Environment=LIVE cannot be combined with Peppol:ReservedExit:Enabled=true. LIVE HTTP stays closed until package 22-b-5 promotes Peppol out of Reserved and ops makes a separate decision.";

    public static readonly EventId RejectedEventId = new(71031, "PeppolStorecoveLiveRejected");

    private readonly ILogger<PeppolStorecoveOptionsValidator> _logger;

    public PeppolStorecoveOptionsValidator(ILogger<PeppolStorecoveOptionsValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ValidateOptionsResult Validate(string? name, PeppolOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var environment = options.Storecove?.Environment?.Trim();
        if (!string.Equals(environment, "LIVE", StringComparison.Ordinal))
            return ValidateOptionsResult.Success;

        if (options.ReservedExit?.Enabled != true)
            return ValidateOptionsResult.Success;

        _logger.LogCritical(
            RejectedEventId,
            "Peppol Storecove LIVE rejected. Reason={Reason}",
            LiveWithReservedExitReason);
        return ValidateOptionsResult.Fail(LiveWithReservedExitReason);
    }
}
