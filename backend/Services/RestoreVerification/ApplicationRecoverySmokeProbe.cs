namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// GET isteği ile yapılandırılmış taban URL’ye duman testi; yalnızca HTTP katmanı (iş mantığı doğrulaması değil).
/// </summary>
public sealed class ApplicationRecoverySmokeProbe : IApplicationRecoverySmokeProbe
{
    public const string HttpClientName = "RestoreVerificationApplicationSmoke";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApplicationRecoverySmokeProbe> _logger;

    public ApplicationRecoverySmokeProbe(
        IHttpClientFactory httpClientFactory,
        ILogger<ApplicationRecoverySmokeProbe> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ApplicationSmokeProbeOutcome> ProbeAsync(Uri baseUri, string relativePath, TimeSpan timeout, CancellationToken ct)
    {
        if (!string.Equals(baseUri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(baseUri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
        {
            var t0 = DateTimeOffset.UtcNow;
            return new ApplicationSmokeProbeOutcome
            {
                Success = false,
                RequestPath = relativePath,
                Error = "unsupported_uri_scheme",
                StartedAtUtc = t0,
                CompletedAtUtc = t0
            };
        }

        var path = string.IsNullOrWhiteSpace(relativePath) ? "/" : relativePath.TrimStart('/');
        var combined = new Uri(baseUri, path);
        var startedAt = DateTimeOffset.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, combined);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            sw.Stop();
            var completedAt = DateTimeOffset.UtcNow;
            var ok = (int)response.StatusCode >= 200 && (int)response.StatusCode < 300;
            if (!ok)
                _logger.LogWarning(
                    "Application smoke probe non-success: status={Status}, uri={Uri}",
                    (int)response.StatusCode,
                    combined.GetLeftPart(UriPartial.Path));

            return new ApplicationSmokeProbeOutcome
            {
                Success = ok,
                HttpStatusCode = (int)response.StatusCode,
                DurationMs = sw.ElapsedMilliseconds,
                RequestPath = combined.PathAndQuery,
                Error = ok ? null : "http_status_not_success",
                StartedAtUtc = startedAt,
                CompletedAtUtc = completedAt
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            var completedAt = DateTimeOffset.UtcNow;
            _logger.LogWarning(ex, "Application smoke probe failed for {Uri}", combined.GetLeftPart(UriPartial.Path));
            return new ApplicationSmokeProbeOutcome
            {
                Success = false,
                DurationMs = sw.ElapsedMilliseconds,
                RequestPath = combined.PathAndQuery,
                Error = ex.Message,
                StartedAtUtc = startedAt,
                CompletedAtUtc = completedAt
            };
        }
    }
}
