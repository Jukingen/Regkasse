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
        sentryEvent.SetExtra("phone", "+431234567");
        sentryEvent.SetExtra("name", "Max");
        sentryEvent.SetExtra("address", "Hauptstrasse 1");
        sentryEvent.SetExtra("iban", "AT00");
        sentryEvent.SetExtra("authorization", "Bearer secret");
        sentryEvent.SetExtra("exception_message", "boom");
        sentryEvent.SetExtra("breadcrumb", "clicked");
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
        Assert.False(sentryEvent.Extra.ContainsKey("phone"));
        Assert.False(sentryEvent.Extra.ContainsKey("name"));
        Assert.False(sentryEvent.Extra.ContainsKey("address"));
        Assert.False(sentryEvent.Extra.ContainsKey("iban"));
        Assert.False(sentryEvent.Extra.ContainsKey("authorization"));
        Assert.False(sentryEvent.Extra.ContainsKey("exception_message"));
        Assert.False(sentryEvent.Extra.ContainsKey("breadcrumb"));
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

    [Fact]
    public void BeforeSendTransaction_ScrubsIdentityFields_AndDropsHealth()
    {
        var health = new SentryTransaction("GET /health/live", "http.server")
        {
            Request = new SentryRequest { Url = "https://api.regkasse.at/health/live" },
        };
        Assert.Null(SentryPiiScrubber.BeforeSendTransaction(health, new SentryHint()));

        var transaction = new SentryTransaction("GET /api/public/customer/dashboard", "http.server")
        {
            User = new SentryUser { Id = "user-1", Email = "cashier@example.at", Username = "max" },
            Request = new SentryRequest
            {
                Url = "https://api.regkasse.at/api/public/customer/dashboard",
                QueryString = "?phone=43123456789&status=ok",
            },
            Description = "lookup cashier@example.at",
        };
        transaction.SetData("phone", "+43123456789");
        transaction.SetData("userId", "user-1");
        transaction.SetTag("authorization", "Bearer secret");
        transaction.SetTag("endpoint", "/api/public/customer/dashboard");

        var sent = SentryPiiScrubber.BeforeSendTransaction(transaction, new SentryHint());

        Assert.NotNull(sent);
        Assert.Null(sent.User.Email);
        Assert.Null(sent.User.Username);
        Assert.Equal("user-1", sent.User.Id);
        Assert.False(sent.Data.ContainsKey("phone"));
        Assert.Equal("user-1", sent.Data["userId"]);
        Assert.False(sent.Tags.ContainsKey("authorization"));
        Assert.Equal("/api/public/customer/dashboard", sent.Tags["endpoint"]);
        Assert.DoesNotContain("cashier@example.at", sent.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone=", sent.Request.QueryString, StringComparison.OrdinalIgnoreCase);
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
