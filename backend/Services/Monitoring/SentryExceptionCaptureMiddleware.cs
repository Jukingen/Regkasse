using Sentry;

namespace KasseAPI_Final.Services.Monitoring;

/// <summary>
/// Global unhandled-exception capture for Sentry. Rethrows so API error bodies stay unchanged.
/// Health probes and <c>/metrics</c> are not reported.
/// </summary>
internal sealed class SentryExceptionCaptureMiddleware
{
    private readonly RequestDelegate _next;

    public SentryExceptionCaptureMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            if (!SentryPiiScrubber.IsIgnoredPath(context.Request.Path.Value))
                SentrySdk.CaptureException(exception);
            throw;
        }
    }
}
