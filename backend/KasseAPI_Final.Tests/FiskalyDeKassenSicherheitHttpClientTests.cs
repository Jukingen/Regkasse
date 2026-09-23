using System.Net;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Countries.KassenSicherheit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyDeKassenSicherheitHttpClientTests
{
    private const string Secret = "secret-value";
    private const string Pin = "pin-value";

    [Fact]
    public async Task StartFinishAndExport_UseBearer_AndDoNotLogSecrets()
    {
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/auth", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"access_token":"tok-1","access_token_expires_in":120}""");
            }

            if (path.Contains("/tx/", StringComparison.Ordinal) && request.Method == HttpMethod.Put)
            {
                var query = request.RequestUri?.Query ?? string.Empty;
                Assert.Contains("tx_revision=", query, StringComparison.Ordinal);
                return Json(HttpStatusCode.OK, """{"state":"ACTIVE","latest_revision":1,"signature":{"value":"sig-1"}}""");
            }

            if (path.EndsWith("/export", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"export_id":"exp-1"}""");
            }

            return Json(HttpStatusCode.OK, """{"state":"ok"}""");
        });
        var logs = new CollectingLogger();
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var client = new FiskalyDeKassenSicherheitHttpClient(http, Options(), logs);

        var start = await client.StartTransactionAsync(new KassenSicherheitStartTransactionRequest(
            Guid.NewGuid(), "tss-1", "client-1", "tx-1", 1));
        var finish = await client.FinishTransactionAsync(new KassenSicherheitFinishTransactionRequest(
            Guid.NewGuid(), "tss-1", "client-1", "tx-1", 2, "process"));
        var export = await client.ExportDsfinvkAsync(new KassenSicherheitExportRequest(Guid.NewGuid(), "tss-1"));

        Assert.True(start.Completed);
        Assert.Equal("sig-1", start.Signature);
        Assert.Equal("ACTIVE", start.State);
        Assert.True(finish.Completed);
        Assert.Equal(1, finish.TxRevision);
        Assert.True(export.Exported);
        Assert.Equal("exp-1", export.ExportId);
        Assert.Equal(1, handler.Requests.Count(r => r.Uri.AbsolutePath.EndsWith("/auth", StringComparison.Ordinal)));
        Assert.Contains(handler.Requests, r => r.Uri.Query.Contains("tx_revision=1", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, r => r.Uri.Query.Contains("tx_revision=2", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, r => r.Body.Contains("\"state\":\"FINISHED\"", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, r => r.Body.Contains("\"type\":\"dsfinvk\"", StringComparison.Ordinal));
        Assert.All(handler.Requests.Where(r => !r.Uri.AbsolutePath.EndsWith("/auth", StringComparison.Ordinal)), r =>
            Assert.Equal("Bearer", r.AuthorizationScheme));
        var logged = string.Join('\n', logs.Messages);
        Assert.DoesNotContain(Secret, logged, StringComparison.Ordinal);
        Assert.DoesNotContain(Pin, logged, StringComparison.Ordinal);
        Assert.DoesNotContain("tok-1", logged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAndInitializeTss_SendsAdminAuthWithoutLoggingPin()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """{"access_token":"tok-2","access_token_expires_in":90,"state":"INITIALIZED"}"""));
        var logs = new CollectingLogger();
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var client = new FiskalyDeKassenSicherheitHttpClient(http, Options(), logs);

        var tss = await client.CreateAndInitializeTssAsync(
            new KassenSicherheitCreateTssRequest(Guid.NewGuid(), "tss-9", "dev"));

        Assert.Equal("INITIALIZED", tss.State);
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Put && r.Uri.AbsolutePath.EndsWith("/tss/tss-9", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post && r.Uri.AbsolutePath.EndsWith("/admin/auth", StringComparison.Ordinal) && r.Body.Contains(Pin, StringComparison.Ordinal));
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Patch);
        Assert.DoesNotContain(Pin, string.Join('\n', logs.Messages), StringComparison.Ordinal);
    }

    private static IOptions<KassenSicherheitOptions> Options() =>
        Microsoft.Extensions.Options.Options.Create(new KassenSicherheitOptions
        {
            Provider = "fiskaly-de",
            Environment = "TEST",
            ApiBaseUrl = "https://kassensichv-middleware.fiskaly.com/api/v2",
            ApiKey = "key-1",
            ApiSecret = Secret,
            AdminPin = Pin,
            AllowSimulatedTse = false,
            HttpTimeoutSeconds = 5,
        });

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _next;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> next) => _next = next;

        public List<RecordedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri ?? new Uri("https://invalid.local/"),
                body,
                request.Headers.Authorization?.Scheme));
            return _next(request);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body, string? AuthorizationScheme);

    private sealed class CollectingLogger : ILogger<FiskalyDeKassenSicherheitHttpClient>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
