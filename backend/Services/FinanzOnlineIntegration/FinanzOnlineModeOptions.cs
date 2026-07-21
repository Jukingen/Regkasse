namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Root <c>FinanzOnline:Mode</c> (AGENTS / ops). Transport still uses nested <c>UseSimulation</c> flags;
/// this value drives outbox <see cref="FinanzOnlineIntegrationMode"/> and cutover eligibility.
/// </summary>
public sealed class FinanzOnlineModeOptions
{
    public const string SectionName = "FinanzOnline";

    /// <summary>
    /// <c>Simulation</c> | <c>Test</c> | <c>Production</c> (aliases: <c>Prod</c>).
    /// Simulation and Test enqueue TEST-mode outbox messages; Production requires cutover guard.
    /// </summary>
    public string Mode { get; set; } = "Test";
}

/// <summary>Maps config / operator mode strings to outbox SOAP intent.</summary>
public static class FinanzOnlineModeResolver
{
    public const string Simulation = "Simulation";
    public const string Test = "Test";
    public const string Production = "Production";

    public static bool IsSimulation(string? mode) =>
        string.Equals(mode, Simulation, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Sim", StringComparison.OrdinalIgnoreCase);

    public static bool IsProduction(string? mode) =>
        string.Equals(mode, Production, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "Prod", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves outbox mode. Simulation/Test → TEST. Production → PROD when cutover allows; otherwise throws.
    /// </summary>
    public static FinanzOnlineIntegrationMode ResolveOutboxMode(
        string? configuredMode,
        FinanzOnlineCutoverGuardOptions cutover,
        out string normalizedDisplayName)
    {
        if (IsProduction(configuredMode))
        {
            normalizedDisplayName = Production;
            var approved = cutover.AllowProdMode &&
                           (!cutover.RequireExplicitProdApproval || !string.IsNullOrWhiteSpace(cutover.ProdApprovalToken));
            if (!approved)
                throw new InvalidOperationException("PROD mode is blocked by cutover guard configuration.");
            return FinanzOnlineIntegrationMode.PROD;
        }

        if (IsSimulation(configuredMode))
        {
            normalizedDisplayName = Simulation;
            return FinanzOnlineIntegrationMode.TEST;
        }

        normalizedDisplayName = Test;
        return FinanzOnlineIntegrationMode.TEST;
    }

    /// <summary>UI / company-config Environment label (Test | Production | Simulation).</summary>
    public static string ToConfigEnvironmentLabel(string? configuredMode)
    {
        if (IsProduction(configuredMode))
            return Production;
        if (IsSimulation(configuredMode))
            return Simulation;
        return Test;
    }
}
