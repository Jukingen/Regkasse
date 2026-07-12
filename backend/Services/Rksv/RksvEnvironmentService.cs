using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services.Rksv;

public interface IRksvEnvironmentService
{
    bool IsDemoMode();

    bool IsProductionMode();

    bool IsTseSimulated();

    bool ShowDemoLabel();

    string GetEnvironmentDisplayName();

    /// <summary>Long-form TSE status for report detail rows.</summary>
    string GetTseStatusDisplay();

    /// <summary>Short badge label: TSE AKTIV / TSE SIMULIERT.</summary>
    string GetTseStatusBadge();

    /// <summary>Multi-line RKSV footer block for daily closing print/PDF.</summary>
    string GetRksvFooter();
}

public sealed class RksvEnvironmentService : IRksvEnvironmentService
{
    internal const string DemoFooter =
        """
        ═══════════════════════════════════════════
           ⚠️ DEMO / NICHT FISKAL
           Dieser Bericht ist nur zu Testzwecken.
           TSE: SIMULIERT
        ═══════════════════════════════════════════
        """;

    internal const string ProductionFooter =
        """
        ═══════════════════════════════════════════
           Registrierkassensicherheitsverordnung (RKSV)
           Dieser Tagesabschluss ist fiskalisch gültig.
           TSE-Signatur: GEPRÜFT ✅
        ═══════════════════════════════════════════
        """;

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public RksvEnvironmentService(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public bool IsDemoMode()
    {
        return string.Equals(_configuration["RKSV:Mode"], "Demo", StringComparison.OrdinalIgnoreCase)
               || _environment.IsDevelopment()
               || _environment.IsStaging();
    }

    public bool IsProductionMode() => !IsDemoMode();

    public bool IsTseSimulated() =>
        IsDemoMode()
        || string.Equals(_configuration["RKSV:TseMode"], "Simulation", StringComparison.OrdinalIgnoreCase);

    public bool ShowDemoLabel() =>
        IsDemoMode()
        && _configuration.GetValue("RKSV:ShowDemoLabel", true);

    public string GetEnvironmentDisplayName() =>
        IsDemoMode() ? "🧪 DEMO / TEST" : "🚀 PRODUCTION";

    public string GetTseStatusDisplay() =>
        IsTseSimulated()
            ? "TSE: SIMULIERT (NUR TEST)"
            : "TSE: AKTIV ✅";

    public string GetTseStatusBadge() =>
        IsTseSimulated()
            ? "TSE SIMULIERT"
            : "TSE AKTIV";

    public string GetRksvFooter() =>
        FormatFooter(IsDemoMode());

    internal static string FormatFooter(bool isDemoFiscal) =>
        isDemoFiscal ? DemoFooter.Trim() : ProductionFooter.Trim();
}
