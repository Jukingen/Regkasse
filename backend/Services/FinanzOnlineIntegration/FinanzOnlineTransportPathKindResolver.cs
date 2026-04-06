using System;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Classifies operator-visible FinanzOnline transport path: simulated adapters vs real SOAP (TEST/PROD intent from stored message mode).
/// </summary>
public static class FinanzOnlineTransportPathKindResolver
{
    public const string Simulated = "Simulated";
    public const string RealTest = "RealTest";
    public const string RealProduction = "RealProduction";

    /// <summary>
    /// When any FO transport layer uses simulation, all pipeline results are simulated regardless of stored <paramref name="storedMessageMode"/>.
    /// Otherwise TEST vs PROD follows the persisted outbox message mode.
    /// </summary>
    public static string Resolve(bool anyLayerUsesSimulation, string? storedMessageMode)
    {
        if (anyLayerUsesSimulation)
            return Simulated;

        var m = (storedMessageMode ?? "TEST").Trim();
        if (string.Equals(m, "PROD", StringComparison.OrdinalIgnoreCase))
            return RealProduction;

        return RealTest;
    }
}
