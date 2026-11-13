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
        var sampleRate = builder.Configuration.GetValue("Sentry:TracesSampleRate", SentryPiiScrubber.DefaultTracesSampleRate);
        if (sampleRate is < 0 or > 1)
            sampleRate = SentryPiiScrubber.DefaultTracesSampleRate;

        var environment = builder.Environment.EnvironmentName;
        var release = typeof(SentryHost).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        builder.WebHost.UseSentry(options =>
        {
            options.Dsn = dsn;
            options.Environment = environment;
            options.Release = release;
            options.TracesSampleRate = sampleRate;
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
