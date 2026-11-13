using System.Globalization;

namespace KasseAPI_Final.Services.Monitoring;

/// <summary>
/// Resolves Sentry error and transaction sample rates.
/// Environment variables win over configuration, then the built-in default.
/// Out-of-range or unparseable values are ignored.
/// </summary>
internal static class SentrySampling
{
    internal const string TracesSampleRateVariable = "SENTRY_TRACES_SAMPLE_RATE";
    internal const string ErrorSampleRateVariable = "SENTRY_SAMPLE_RATE";

    /// <summary>Fraction of transactions sent to Sentry.</summary>
    internal const double DefaultTracesSampleRate = 0.1;

    /// <summary>
    /// Fraction of error events sent to Sentry.
    /// Raise <c>SENTRY_SAMPLE_RATE</c> to 1.0 when error volume is low.
    /// </summary>
    internal const double DefaultErrorSampleRate = 0.5;

    internal static double Resolve(string? environmentValue, string? configuredValue, double fallback)
    {
        if (TryParseUnitInterval(environmentValue, out var fromEnvironment))
            return fromEnvironment;
        if (TryParseUnitInterval(configuredValue, out var fromConfiguration))
            return fromConfiguration;
        return fallback;
    }

    internal static bool TryParseUnitInterval(string? raw, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return false;
        return value is >= 0 and <= 1;
    }
}
