using KasseAPI_Final.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Shared evaluation of TSE / RKSV / FinanzOnline fiscal config for Production (and optional Staging) lock.
/// </summary>
public static class TseFiscalConfigLockEvaluator
{
    public const string ReasonTseModeOffOrDemo = "Tse:TseMode must be Device in Production (Off/Demo are forbidden).";
    public const string ReasonTseModeFake = "Tse:Mode=Fake is forbidden in Production.";
    public const string ReasonProviderNotRealVendor =
        "Tse:Provider must be a real vendor in Production (fiskaly, epson, or swissbit).";
    public const string ReasonSimulatedDailyClosing = "Tse:AllowSimulatedDailyClosing must be false in Production.";
    public const string ReasonFallbackEnabled = "Tse:FallbackEnabled must be false in Production (Development-only Soft TSE fallback).";
    public const string ReasonSoftTseEnabled = "Tse:SoftTseEnabled must be false in Production.";
    public const string ReasonRksvTseSimulation = "RKSV:TseMode=Simulation is forbidden in Production.";
    public const string ReasonRksvModeNotProduction = "RKSV:Mode must be Production in Production.";
    public const string ReasonFinanzOnlineSimulation =
        "FinanzOnline:UseSimulation must be false in Production (Session/Registrierkassen/TransmissionQuery/Mode).";

    /// <summary>Result of evaluating whether fiscal TSE configuration is safe for the host environment.</summary>
    public sealed record Result(
        bool LockApplies,
        bool IsSafe,
        bool EscapeHatchActive,
        IReadOnlyList<string> Reasons)
    {
        public bool Ok => !LockApplies || IsSafe || EscapeHatchActive;
    }

    public static bool LockAppliesToHost(IHostEnvironment environment, TseOptions options)
    {
        if (environment.IsProduction())
            return true;
        if (environment.IsStaging() && options.EnforceProductionLockInStaging)
            return true;
        return false;
    }

    public static Result Evaluate(
        IHostEnvironment environment,
        IConfiguration configuration,
        TseOptions options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        if (!LockAppliesToHost(environment, options))
        {
            return new Result(
                LockApplies: false,
                IsSafe: true,
                EscapeHatchActive: false,
                Reasons: Array.Empty<string>());
        }

        var reasons = CollectViolations(configuration, options);
        var escape = options.AllowUnsafeFiscalModesInProduction;
        var safe = reasons.Count == 0;
        return new Result(
            LockApplies: true,
            IsSafe: safe,
            EscapeHatchActive: escape && !safe,
            Reasons: reasons);
    }

    public static bool IsRealVendorProvider(string? provider)
    {
        var normalized = TseOptions.NormalizeProviderName(provider);
        return normalized is TseOptions.ProviderFiskaly
            or TseOptions.ProviderEpson
            or TseOptions.ProviderSwissbit;
    }

    /// <summary>True when any FinanzOnline transport surface is configured for simulation.</summary>
    public static bool IsFinanzOnlineSimulated(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.GetValue("FinanzOnline:Session:UseSimulation", false))
            return true;
        if (configuration.GetValue("FinanzOnline:Registrierkassen:UseSimulation", false))
            return true;
        if (configuration.GetValue("FinanzOnline:TransmissionQuery:UseSimulation", false))
            return true;

        var mode = configuration["FinanzOnline:Mode"];
        return string.Equals(mode, "Simulation", StringComparison.OrdinalIgnoreCase);
    }

    public static List<string> CollectViolations(IConfiguration configuration, TseOptions options)
    {
        var reasons = new List<string>();

        if (options.IsOff
            || string.Equals(options.TseMode, "Demo", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(ReasonTseModeOffOrDemo);
        }

        if (options.IsFakeSigningMode)
            reasons.Add(ReasonTseModeFake);

        if (!IsRealVendorProvider(options.Provider))
            reasons.Add(ReasonProviderNotRealVendor);

        if (options.AllowSimulatedDailyClosing)
            reasons.Add(ReasonSimulatedDailyClosing);

        if (options.FallbackEnabled)
            reasons.Add(ReasonFallbackEnabled);

        if (options.SoftTseEnabled)
            reasons.Add(ReasonSoftTseEnabled);

        if (string.Equals(configuration["RKSV:TseMode"], "Simulation", StringComparison.OrdinalIgnoreCase))
            reasons.Add(ReasonRksvTseSimulation);

        if (!string.Equals(configuration["RKSV:Mode"], "Production", StringComparison.OrdinalIgnoreCase))
            reasons.Add(ReasonRksvModeNotProduction);

        if (IsFinanzOnlineSimulated(configuration))
            reasons.Add(ReasonFinanzOnlineSimulation);

        return reasons;
    }
}
