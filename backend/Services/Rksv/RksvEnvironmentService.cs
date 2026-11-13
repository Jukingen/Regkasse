namespace KasseAPI_Final.Services.Rksv;

public interface IRksvEnvironmentService
{
    bool IsDemoMode();

    bool IsProductionMode();

    bool IsTseSimulated();

    bool ShowDemoLabel();

    /// <summary>True when RKSV FinanzOnline overlay is Simulation or FinanzOnline:* simulation is active.</summary>
    bool IsFinanzOnlineSimulated();

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
    private readonly IRksvRuntimeConfigService? _runtimeConfig;

    public RksvEnvironmentService(
        IConfiguration configuration,
        IHostEnvironment environment,
        IRksvRuntimeConfigService? runtimeConfig = null)
    {
        _configuration = configuration;
        _environment = environment;
        _runtimeConfig = runtimeConfig;
    }

    public bool IsDemoMode()
    {
        var overlay = TryOverlay();
        if (overlay != null)
            return overlay.IsDemoMode;

        return string.Equals(_configuration["RKSV:Mode"], "Demo", StringComparison.OrdinalIgnoreCase)
               || _environment.IsDevelopment()
               || _environment.IsStaging();
    }

    public bool IsProductionMode() => !IsDemoMode();

    public bool IsTseSimulated() =>
        IsDemoMode()
        || (TryOverlay()?.IsTseSimulation ?? false)
        || string.Equals(_configuration["RKSV:TseMode"], "Simulation", StringComparison.OrdinalIgnoreCase);

    public bool ShowDemoLabel()
    {
        var overlay = TryOverlay();
        if (overlay != null)
            return overlay.ShowDemoLabel;

        return IsDemoMode()
               && _configuration.GetValue("RKSV:ShowDemoLabel", true);
    }

    public bool IsFinanzOnlineSimulated()
    {
        var overlay = TryOverlay();
        if (overlay != null && overlay.IsFinanzOnlineSimulation)
            return true;

        return Tse.TseFiscalConfigLockEvaluator.IsFinanzOnlineSimulated(_configuration);
    }

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
        FormatFooter(ShowDemoLabel());

    internal static string FormatFooter(bool isDemoFiscal) =>
        isDemoFiscal ? DemoFooter.Trim() : ProductionFooter.Trim();

    private RksvRuntimeSnapshot? TryOverlay()
    {
        try
        {
            return _runtimeConfig?.GetEffective();
        }
        catch
        {
            return null;
        }
    }
}
