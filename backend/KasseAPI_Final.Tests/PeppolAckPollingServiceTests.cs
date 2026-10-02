using System.Net;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Countries.EInvoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PeppolAckPollingServiceTests
{
    private const string MessageId = "sc-guid-ack";

    [Fact]
    public async Task Delivered_MatchingGuid_SetsAck()
    {
        var harness = await HarnessAsync(
            """{"guid":"sc-guid-ack","state":"DELIVERED"}""",
            attemptedAt: DateTime.UtcNow.AddMinutes(-10));

        await harness.Service.PollOnceAsync();

        var row = await harness.Db.EinvoiceSubmissions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(EinvoiceSubmissionStatuses.Ack, row.Status);
        Assert.NotNull(row.AckedAtUtc);
        Assert.Null(row.FailureReason);
        Assert.Equal("DELIVERED", row.ProviderStatus);
        Assert.Equal(1, harness.Handler.Calls);
        harness.Audit.Verify(audit => audit.LogSystemOperationAsync(
            "EINVOICE_ACK_RECEIVED",
            "EinvoiceSubmission",
            "system",
            "SuperAdmin",
            "Canary e-invoice acknowledged",
            null,
            AuditLogStatus.Success,
            null,
            null,
            null,
            null,
            null,
            AuditEventType.EinvoiceAckReceived,
            It.IsAny<Guid?>(),
            harness.TenantId,
            null,
            null,
            null), Times.Once);
        harness.Activity.Verify(activity => activity.TryPublishAsync(
            harness.TenantId,
            ActivityEventType.EinvoiceAckReceived,
            It.IsAny<object?>(),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(115, (int)AuditEventType.EinvoiceAckReceived);
        Assert.Equal(262, (int)ActivityEventType.EinvoiceAckReceived);
    }

    [Fact]
    public async Task ErrorState_SetsFailed_WithoutResponseBody()
    {
        var harness = await HarnessAsync(
            """{"guid":"sc-guid-ack","state":"ERROR","message":"buyer-pii-secret"}""",
            attemptedAt: DateTime.UtcNow.AddMinutes(-10));

        await harness.Service.PollOnceAsync();

        var row = await harness.Db.EinvoiceSubmissions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(EinvoiceSubmissionStatuses.Failed, row.Status);
        Assert.Equal(PeppolAckPollingService.AckErrorReason, row.FailureReason);
        Assert.DoesNotContain("buyer-pii-secret", row.FailureReason, StringComparison.Ordinal);
        Assert.Null(row.AckedAtUtc);
        Assert.NotEqual(EinvoiceSubmissionStatuses.Ack, row.Status);
    }

    [Fact]
    public async Task OlderThan24Hours_SetsTimeout_WithoutHttp()
    {
        var harness = await HarnessAsync(
            """{"guid":"sc-guid-ack","state":"DELIVERED"}""",
            attemptedAt: DateTime.UtcNow.AddHours(-25),
            throwOnHttp: true);

        await harness.Service.PollOnceAsync();

        var row = await harness.Db.EinvoiceSubmissions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(EinvoiceSubmissionStatuses.Failed, row.Status);
        Assert.Equal(PeppolAckPollingService.AckTimeoutReason, row.FailureReason);
        Assert.Equal(0, harness.Handler.Calls);
        Assert.Null(row.AckedAtUtc);
    }

    [Fact]
    public async Task StatusCodeOnly_StaysSent()
    {
        var harness = await HarnessAsync(
            """{"status":"200"}""",
            attemptedAt: DateTime.UtcNow.AddMinutes(-10));

        await harness.Service.PollOnceAsync();

        var row = await harness.Db.EinvoiceSubmissions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(EinvoiceSubmissionStatuses.Sent, row.Status);
        Assert.Null(row.AckedAtUtc);
    }

    private static async Task<Harness> HarnessAsync(
        string responseBody,
        DateTime attemptedAt,
        bool throwOnHttp = false)
    {
        var tenantId = Guid.NewGuid();
        var dbName = $"PeppolAck_{Guid.NewGuid():N}";
        await using (var seed = CreateDb(dbName))
        {
            seed.EinvoiceSubmissions.Add(new EinvoiceSubmission
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                InvoiceId = Guid.NewGuid(),
                Status = EinvoiceSubmissionStatuses.Sent,
                CorrelationId = Guid.NewGuid(),
                ProviderMessageId = MessageId,
                AttemptedAtUtc = attemptedAt,
                CreatedAtUtc = attemptedAt,
            });
            await seed.SaveChangesAsync();
        }

        var handler = new RecordingHandler(_ =>
        {
            if (throwOnHttp)
                throw new InvalidOperationException("socket");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        });
        var options = Options.Create(new PeppolOptions
        {
            Provider = "storecove",
            AckPollInterval = 5,
            ReservedExit = new PeppolReservedExitOptions
            {
                Enabled = true,
                CanaryTenantId = tenantId.ToString("D"),
            },
            Storecove = new PeppolStorecoveOptions
            {
                ApiKey = "test-key",
                BaseUrl = "https://api.storecove.test/api/v2/",
                Environment = "TEST",
            },
        });
        var http = new HttpClient(handler);
        var client = new StorecovePeppolAccessPointClient(options, http, new NamedDbFactory(dbName));
        var audit = new Mock<IAuditLogService>();
        audit.Setup(item => item.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(item => item.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(new NamedDbFactory(dbName));
        services.AddSingleton(client);
        services.AddSingleton(audit.Object);
        services.AddSingleton(activity.Object);
        var provider = services.BuildServiceProvider();
        var service = new PeppolAckPollingService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedMonitor(options.Value),
            NullLogger<PeppolAckPollingService>.Instance);
        return new Harness(CreateDb(dbName), service, handler, audit, activity, tenantId);
    }

    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private sealed record Harness(
        AppDbContext Db,
        PeppolAckPollingService Service,
        RecordingHandler Handler,
        Mock<IAuditLogService> Audit,
        Mock<IActivityEventPublisher> Activity,
        Guid TenantId);

    private sealed class FixedMonitor : IOptionsMonitor<PeppolOptions>
    {
        public FixedMonitor(PeppolOptions value) => CurrentValue = value;

        public PeppolOptions CurrentValue { get; }

        public PeppolOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<PeppolOptions, string?> listener) => null;
    }

    private sealed class NamedDbFactory : IDbContextFactory<AppDbContext>
    {
        private readonly string _name;

        public NamedDbFactory(string name) => _name = name;

        public AppDbContext CreateDbContext() => CreateDb(_name);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDb(_name));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> send) => _send = send;

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_send(request));
        }
    }
}
