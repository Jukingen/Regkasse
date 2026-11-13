using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;

namespace KasseAPI_Final.Services.Monitoring;

/// <summary>
/// Wires Sentry on the web host. An empty <c>Sentry:Dsn</c> leaves the SDK disabled and does not throw.
/// </summary>
internal static class SentryHost
{
    internal static void UseRegkasseSentry(this WebApplicationBuilder builder, IDiagnosticLogger? diagnosticLogger = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var dsn = builder.Configuration["Sentry:Dsn"]?.Trim() ?? string.Empty;
        var tracesSampleRate = SentrySampling.Resolve(
            Environment.GetEnvironmentVariable(SentrySampling.TracesSampleRateVariable),
            builder.Configuration["Sentry:TracesSampleRate"],
            SentrySampling.DefaultTracesSampleRate);
        var errorSampleRate = SentrySampling.Resolve(
            Environment.GetEnvironmentVariable(SentrySampling.ErrorSampleRateVariable),
            builder.Configuration["Sentry:SampleRate"],
            SentrySampling.DefaultErrorSampleRate);

        var environment = builder.Environment.EnvironmentName;
        var release = typeof(SentryHost).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        builder.WebHost.UseSentry(options =>
        {
            options.Dsn = dsn;
            options.Environment = environment;
            options.Release = release;
            options.TracesSampleRate = tracesSampleRate;
            options.SampleRate = (float)errorSampleRate;
            options.SendDefaultPii = false;
            options.AttachStacktrace = true;
            options.AdjustStandardEnvironmentNameCasing = false;
            options.Debug = false;
            options.TracesSampler = context =>
            {
                var path = context.TryGetHttpPath();
                return SentryPiiScrubber.IsIgnoredPath(path) ? 0 : null;
            };
            options.SetBeforeSend(SentryPiiScrubber.BeforeSend);
            options.SetBeforeSendTransaction(SentryPiiScrubber.BeforeSendTransaction);
            if (diagnosticLogger != null)
                options.DiagnosticLogger = diagnosticLogger;
        });
    }
}
