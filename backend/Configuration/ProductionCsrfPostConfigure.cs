using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Configuration;

/// <summary>
/// Production cannot run with CSRF off. If config still has <c>Enabled=false</c>, turn it on and warn.
/// Development keeps operator control (typically disabled + bypass).
/// </summary>
public sealed class ProductionCsrfPostConfigure : IPostConfigureOptions<CsrfOptions>
{
    public static readonly EventId EnabledByDefaultEventId = new(73001, "CsrfEnabledByDefaultInProduction");

    private readonly IHostEnvironment _environment;
    private readonly ILogger<ProductionCsrfPostConfigure> _logger;

    public ProductionCsrfPostConfigure(
        IHostEnvironment environment,
        ILogger<ProductionCsrfPostConfigure> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public void PostConfigure(string? name, CsrfOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!_environment.IsProduction() || options.Enabled)
            return;

        _logger.LogWarning(
            EnabledByDefaultEventId,
            "CSRF is disabled in Production - enabling by default");
        options.Enabled = true;
    }
}
