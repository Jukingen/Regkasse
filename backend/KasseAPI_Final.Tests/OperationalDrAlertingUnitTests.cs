using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// DR uyarı publisher zinciri ve webhook gönderimi birim testleri.
/// </summary>
public sealed class OperationalDrAlertingUnitTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Captured { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Captured = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    [Fact]
    public void CompositeBackupAlertPublisher_invokes_all_publishers()
    {
        var first = new Mock<IBackupAlertPublisher>();
        var second = new Mock<IBackupAlertPublisher>();
        var composite = new CompositeBackupAlertPublisher(new[] { first.Object, second.Object });
        var evt = new BackupAlertEvent(
            BackupAlertKind.BackupFailed,
            Guid.NewGuid(),
            "corr",
            "test");

        composite.Publish(evt);

        first.Verify(p => p.Publish(It.Is<BackupAlertEvent>(e => e.Kind == evt.Kind && e.Message == evt.Message)), Times.Once);
        second.Verify(p => p.Publish(It.Is<BackupAlertEvent>(e => e.Kind == evt.Kind && e.Message == evt.Message)), Times.Once);
    }

    [Fact]
    public void WebhookBackupAlertPublisher_posts_json_when_enabled()
    {
        var handler = new CaptureHandler();
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(WebhookBackupAlertPublisher.HttpClientName))
            .Returns(() => new HttpClient(handler));

        var options = new OperationalDrAlertOptions
        {
            WebhookEnabled = true,
            WebhookUrl = "https://example.test/dr-alerts",
            WebhookTimeoutSeconds = 10
        };
        var monitor = new Mock<IOptionsMonitor<OperationalDrAlertOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);

        var publisher = new WebhookBackupAlertPublisher(
            factory.Object,
            monitor.Object,
            NullLogger<WebhookBackupAlertPublisher>.Instance);

        var runId = Guid.NewGuid();
        publisher.Publish(new BackupAlertEvent(
            BackupAlertKind.StaleRunRecovered,
            runId,
            "c1",
            "lease expired",
            new Dictionary<string, string> { ["phase"] = "running" },
            RestoreVerificationRunId: null));

        Assert.NotNull(handler.Captured);
        Assert.Equal(HttpMethod.Post, handler.Captured!.Method);
        Assert.Equal(options.WebhookUrl, handler.Captured.RequestUri!.ToString());
    }
}
