using KasseAPI_Final.Services.Monitoring;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Sentry;
using Sentry.Extensibility;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Sentry stays off when the DSN is empty and does not warn when a syntactically valid DSN is set.
/// PII fields never remain on the outbound event.
/// </summary>
public sealed class SentryInitializationTests
{
    private static readonly object Gate = new();

    [Fact]
    public async Task EmptyDsn_DoesNotThrow()
    {
        lock (Gate)
        {
            using var app = CreateHost(dsn: "");
            var started = app.StartAsync();
            Assert.True(started.IsCompletedSuccessfully || started.Status == TaskStatus.RanToCompletion || !started.IsFaulted);
            started.GetAwaiter().GetResult();
            app.StopAsync().GetAwaiter().GetResult();
            SentrySdk.Close();
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task DummyDsn_DoesNotLogWarnings()
    {
        var logger = new CollectingDiagnosticLogger();
        lock (Gate)
        {
            using var app = CreateHost(dsn: "https://public@o0.ingest.sentry.io/1", logger);
            app.StartAsync().GetAwaiter().GetResult();
            app.StopAsync().GetAwaiter().GetResult();
            SentrySdk.Close();
        }

        var warnings = logger.Entries.Where(entry => entry.Level >= SentryLevel.Warning).ToArray();
        Assert.True(warnings.Length == 0, string.Join(" | ", warnings.Select(entry => entry.Message)));
        await Task.CompletedTask;
    }

    [Fact]
    public void Scrub_DropsSecretFields_AndKeepsOperatorContext()
    {
        var sentryEvent = new SentryEvent
        {
            Message = "payment failed",
            User = new SentryUser { Id = "user-1", Email = "cashier@example.at" },
        };
        sentryEvent.SetExtra("userId", "user-1");
        sentryEvent.SetExtra("tenantId", "tenant-1");
        sentryEvent.SetExtra("endpoint", "/api/pos/payment");
        sentryEvent.SetExtra("method", "POST");
        sentryEvent.SetExtra("status", 500);
        sentryEvent.SetExtra("email", "cashier@example.at");
        sentryEvent.SetExtra("password", "secret");
        sentryEvent.SetExtra("token", "jwt");
        sentryEvent.SetExtra("voucherCode", "GIFT-1");
        sentryEvent.SetExtra("TseSignature", "jws");
        sentryEvent.Request = new SentryRequest
        {
            Url = "https://api.regkasse.at/api/pos/payment",
            Method = "POST",
            QueryString = "?token=abc&status=500",
            Data = new Dictionary<string, object?>
            {
                ["email"] = "cashier@example.at",
                ["password"] = "secret",
                ["userId"] = "user-1",
            },
        };

        KasseAPI_Final.Services.Monitoring.SentryPiiScrubber.Scrub(sentryEvent);

        Assert.Equal("user-1", sentryEvent.Extra["userId"]);
        Assert.Equal("tenant-1", sentryEvent.Extra["tenantId"]);
        Assert.Equal("/api/pos/payment", sentryEvent.Extra["endpoint"]);
        Assert.Equal("POST", sentryEvent.Extra["method"]);
        Assert.Equal(500, sentryEvent.Extra["status"]);
        Assert.False(sentryEvent.Extra.ContainsKey("email"));
        Assert.False(sentryEvent.Extra.ContainsKey("password"));
        Assert.False(sentryEvent.Extra.ContainsKey("token"));
        Assert.False(sentryEvent.Extra.ContainsKey("voucherCode"));
        Assert.False(sentryEvent.Extra.ContainsKey("TseSignature"));
        Assert.Null(sentryEvent.User.Email);
        Assert.Equal("user-1", sentryEvent.User.Id);

        var data = Assert.IsAssignableFrom<IDictionary<string, object?>>(sentryEvent.Request.Data);
        Assert.False(data.ContainsKey("email"));
        Assert.False(data.ContainsKey("password"));
        Assert.Equal("user-1", data["userId"]);
        Assert.DoesNotContain("token=", sentryEvent.Request.QueryString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status=500", sentryEvent.Request.QueryString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/api/health/live")]
    [InlineData("/metrics")]
    public void IgnoredPaths_AreNotSent(string path)
    {
        var sentryEvent = new SentryEvent
        {
            Request = new SentryRequest { Url = "https://api.regkasse.at" + path },
        };

        var sent = KasseAPI_Final.Services.Monitoring.SentryPiiScrubber.BeforeSend(sentryEvent, new SentryHint());

        Assert.Null(sent);
    }

    private static WebApplication CreateHost(string dsn, IDiagnosticLogger? logger = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            EnvironmentName = "Development",
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Sentry:Dsn"] = dsn;
        builder.UseRegkasseSentry(logger);
        return builder.Build();
    }

    private sealed class CollectingDiagnosticLogger : IDiagnosticLogger
    {
        public List<(SentryLevel Level, string Message)> Entries { get; } = [];

        public bool IsEnabled(SentryLevel logLevel) => logLevel >= SentryLevel.Warning;

        public void Log(SentryLevel logLevel, string message, Exception? exception = null, params object[] args)
        {
            if (logLevel < SentryLevel.Warning)
                return;
            var text = args is { Length: > 0 } ? string.Format(message, args) : message;
            if (exception != null)
                text = text + " " + exception.Message;
            Entries.Add((logLevel, text));
        }
    }
}
