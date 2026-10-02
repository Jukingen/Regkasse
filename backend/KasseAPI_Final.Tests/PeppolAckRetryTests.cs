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

public sealed class PeppolAckRetryTests
{
    private const string MessageId = "sc-guid-retry";

    [Fact]
    public async Task TransientError_SchedulesRetry_AndWaitsForTheInterval()
    {
        var harness = await HarnessAsync(ErrorBody("STORE_INTERNAL"), attemptedAt: DateTime.UtcNow.AddMinutes(-10));

        await harness.Service.PollOnceAsync();

        var row = await ReloadAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Sent, row.Status);
        Assert.Equal(1, row.ProviderAttemptCount);
        Assert.Null(row.FailureReason);
        Assert.Equal(1, harness.Handler.Calls);
        Assert.Equal(ActivitySeverityNames.Warning, ActivityEventSeverityRules.DefaultFor(ActivityEventType.EinvoiceSubmissionRetry));
        Assert.Equal(118, (int)AuditEventType.EinvoiceSubmissionRetry);
        Assert.Equal(263, (int)ActivityEventType.EinvoiceSubmissionRetry);
        harness.Audit.Verify(audit => audit.LogSystemOperationAsync(
            "EINVOICE_SUBMISSION_RETRY",
            "EinvoiceSubmission",
            "system",
            "SuperAdmin",
            "Canary e-invoice status retry scheduled",
            null,
            AuditLogStatus.Success,
            null,
            null,
            null,
            null,
            null,
            AuditEventType.EinvoiceSubmissionRetry,
            It.IsAny<Guid?>(),
            harness.TenantId,
            null,
            null,
            null), Times.Once);

        await harness.Service.PollOnceAsync();

        row = await ReloadAsync(harness.Db);
        Assert.Equal(1, row.ProviderAttemptCount);
        Assert.Equal(1, harness.Handler.Calls);

        row.AttemptedAtUtc = DateTime.UtcNow.AddSeconds(-(PeppolAckPollingService.DefaultRetryIntervalsSeconds[0] + 1));
        await harness.Db.SaveChangesAsync();
        harness.Handler.NextBody = """{"guid":"sc-guid-retry","state":"processing"}""";
        await harness.Service.PollOnceAsync();

        row = await ReloadAsync(harness.Db);
        Assert.Equal(2, harness.Handler.Calls);
        Assert.Equal(EinvoiceSubmissionStatuses.Sent, row.Status);
        Assert.Equal(1, row.ProviderAttemptCount);
    }

    [Fact]
    public async Task SecondTransientError_StaysSent_WithAttemptCountTwo()
    {
        var harness = await HarnessAsync(
            ErrorBody("TIMEOUT"),
            attemptedAt: DateTime.UtcNow.AddSeconds(-(PeppolAckPollingService.DefaultRetryIntervalsSeconds[0] + 1)),
            attemptCount: 1);

        await harness.Service.PollOnceAsync();

        var row = await ReloadAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Sent, row.Status);
        Assert.Equal(2, row.ProviderAttemptCount);
        Assert.Null(row.FailureReason);
        Assert.Equal(1, harness.Handler.Calls);
    }

    [Fact]
    public async Task ThirdTransientError_FailsWhenRetriesAreExhausted()
    {
        var harness = await HarnessAsync(
            ErrorBody("RATE_LIMIT"),
            attemptedAt: DateTime.UtcNow.AddSeconds(-(PeppolAckPollingService.DefaultRetryIntervalsSeconds[1] + 1)),
            attemptCount: 2);

        await harness.Service.PollOnceAsync();

        var row = await ReloadAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Failed, row.Status);
        Assert.Equal(PeppolAckPollingService.RetriesExhaustedReason, row.FailureReason);
        Assert.Equal(3, row.ProviderAttemptCount);
        Assert.DoesNotContain("RATE_LIMIT", row.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonRetryableError_FailsImmediately()
    {
        var harness = await HarnessAsync(
            """{"guid":"sc-guid-retry","state":"REJECTED","code":"SCHEMA_INVALID"}""",
            attemptedAt: DateTime.UtcNow.AddMinutes(-10));

        await harness.Service.PollOnceAsync();

        var row = await ReloadAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Failed, row.Status);
        Assert.Equal(PeppolAckPollingService.AckErrorReason, row.FailureReason);
        Assert.Equal(0, row.ProviderAttemptCount);
        Assert.Null(row.AckedAtUtc);
        Assert.DoesNotContain("SCHEMA_INVALID", row.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeliveredAfterOneRetry_SetsAck()
    {
        var harness = await HarnessAsync(
            """{"guid":"sc-guid-retry","state":"DELIVERED"}""",
            attemptedAt: DateTime.UtcNow.AddSeconds(-(PeppolAckPollingService.DefaultRetryIntervalsSeconds[0] + 1)),
            attemptCount: 1);

        await harness.Service.PollOnceAsync();

        var row = await ReloadAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Ack, row.Status);
        Assert.NotNull(row.AckedAtUtc);
        Assert.Null(row.FailureReason);
        Assert.Equal(1, row.ProviderAttemptCount);
    }

    private static string ErrorBody(string code) =>
        $$"""{"guid":"sc-guid-retry","state":"ERROR","code":"{{code}}"}""";

    private static async Task<Harness> HarnessAsync(string responseBody, DateTime attemptedAt, int attemptCount = 0)
    {
        var tenantId = Guid.NewGuid();
        var dbName = $"PeppolRetry_{Guid.NewGuid():N}";
        var createdAt = DateTime.UtcNow.AddMinutes(-10);
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
                ProviderAttemptCount = attemptCount,
                AttemptedAtUtc = attemptedAt,
                CreatedAtUtc = createdAt,
            });
            await seed.SaveChangesAsync();
        }

        var handler = new RecordingHandler(responseBody);
        var options = Options.Create(new PeppolOptions
        {
            Provider = "storecove",
            AckPollInterval = 5,
            AckRetryIntervalsSeconds = [300, 1800],
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
        return new Harness(CreateDb(dbName), service, handler, audit, tenantId);
    }

    private static async Task<EinvoiceSubmission> ReloadAsync(AppDbContext db)
    {
        db.ChangeTracker.Clear();
        return await db.EinvoiceSubmissions.IgnoreQueryFilters().SingleAsync();
    }

    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private sealed record Harness(
        AppDbContext Db,
        PeppolAckPollingService Service,
        RecordingHandler Handler,
        Mock<IAuditLogService> Audit,
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
        public RecordingHandler(string body) => NextBody = body;

        public int Calls { get; private set; }

        public string NextBody { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(NextBody, Encoding.UTF8, "application/json"),
            });
        }
    }
}
