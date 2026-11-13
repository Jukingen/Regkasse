using System.Net.Http;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests.Countries.EInvoicing;

/// <summary>
/// Paket 22-b-4. Real submit and ACK services, mocked Access Point. No Storecove socket.
/// </summary>
public sealed class PeppolCanaryEndToEndTests
{
    private const string ProviderMessageId = "sc-e2e-guid";
    private const string ResponseBodyMarker = "buyer-pii-secret";

    [Fact]
    public async Task Canary_SubmitThenPoll_WritesSentThenAck()
    {
        const int deliverAfterPolls = 2;
        var harness = await HarnessAsync(new Script(ProviderMessageId, deliverAfterPolls, Error: false));

        await harness.Submission.SubmitAsync(
            harness.TenantId,
            Document(),
            invoiceId: harness.InvoiceId);

        var sent = await ReloadAsync(harness.Db);
        Assert.Equal("DE", await CountryAsync(harness.Db));
        Assert.Equal(EinvoiceSubmissionStatuses.Sent, sent.Status);
        Assert.Equal(ProviderMessageId, sent.ProviderMessageId);
        Assert.NotNull(sent.AttemptedAtUtc);
        Assert.Null(sent.FailureReason);
        Assert.Null(sent.AckedAtUtc);
        harness.AccessPoint.Verify(
            client => client.SubmitAsync(It.IsAny<PeppolSubmitRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        harness.AccessPoint.Verify(
            client => client.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        await AgeAsync(harness.Db, TimeSpan.FromMinutes(PeppolAckPollingService.MinAgeMinutes + 1));

        for (var poll = 1; poll < deliverAfterPolls; poll++)
        {
            await harness.Poller.PollOnceAsync();
            var pending = await ReloadAsync(harness.Db);
            Assert.Equal(EinvoiceSubmissionStatuses.Sent, pending.Status);
            Assert.Null(pending.AckedAtUtc);
        }

        await harness.Poller.PollOnceAsync();

        var acked = await ReloadAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Ack, acked.Status);
        Assert.NotNull(acked.AckedAtUtc);
        Assert.Null(acked.FailureReason);
        Assert.Equal(ProviderMessageId, acked.ProviderMessageId);
        harness.AccessPoint.Verify(
            client => client.GetStatusAsync(ProviderMessageId, It.IsAny<CancellationToken>()),
            Times.Exactly(deliverAfterPolls));

        var audits = await harness.Db.AuditLogs.IgnoreQueryFilters().ToListAsync();
        Assert.Contains(audits, row => row.ActionType == AuditEventType.EinvoiceSubmitted);
        Assert.Contains(audits, row => row.ActionType == AuditEventType.EinvoiceAckReceived);
        Assert.Equal(115, (int)AuditEventType.EinvoiceAckReceived);
        Assert.Contains(FeatureFlagNames.EInvoicingPeppol, FeatureFlagNames.Reserved);
    }

    [Fact]
    public async Task StorecoveErrorState_SetsFailed_WithAckError()
    {
        var harness = await HarnessAsync(new Script(ProviderMessageId, DeliverAfterPolls: 1, Error: true));
        await harness.Submission.SubmitAsync(harness.TenantId, Document(), invoiceId: harness.InvoiceId);
        await AgeAsync(harness.Db, TimeSpan.FromMinutes(PeppolAckPollingService.MinAgeMinutes + 1));

        await harness.Poller.PollOnceAsync();

        var row = await ReloadAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Failed, row.Status);
        Assert.Equal(PeppolAckPollingService.AckErrorReason, row.FailureReason);
        Assert.DoesNotContain(ResponseBodyMarker, row.FailureReason, StringComparison.Ordinal);
        Assert.Null(row.AckedAtUtc);
        Assert.NotEqual(EinvoiceSubmissionStatuses.Ack, row.Status);
    }

    [Fact]
    public async Task OlderThan24Hours_SetsTimeout_WithoutStatusCall()
    {
        var harness = await HarnessAsync(new Script(ProviderMessageId, DeliverAfterPolls: 1, Error: false));
        await harness.Submission.SubmitAsync(harness.TenantId, Document(), invoiceId: harness.InvoiceId);
        await AgeAsync(harness.Db, PeppolAckPollingService.AckTimeout + TimeSpan.FromHours(1));

        await harness.Poller.PollOnceAsync();

        var row = await ReloadAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Failed, row.Status);
        Assert.Equal(PeppolAckPollingService.AckTimeoutReason, row.FailureReason);
        Assert.Null(row.AckedAtUtc);
        harness.AccessPoint.Verify(
            client => client.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MissingLegalEntityId_SetsFailed_WithoutHttp()
    {
        var tenantId = Guid.NewGuid();
        var dbName = $"PeppolE2E_{Guid.NewGuid():N}";
        var invoiceId = await SeedAsync(tenantId, dbName, legalEntityId: null);
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("socket"));
        var options = Options.Create(OptionsFor(tenantId, "TEST"));
        var http = new HttpClient(handler);
        var storecove = new StorecovePeppolAccessPointClient(options, http, new NamedDbFactory(dbName, tenantId));
        await using var db = CreateDb(tenantId, dbName);
        var submission = Submission(db, options, canaryTransport: null, storecove: storecove, audit: AuditThatWrites(new NamedDbFactory(dbName, tenantId)));

        await submission.SubmitAsync(tenantId, Document(), invoiceId: invoiceId);

        var row = await ReloadAsync(db);
        Assert.Equal(EinvoiceSubmissionStatuses.Failed, row.Status);
        Assert.Equal(StorecovePeppolAccessPointClient.ParticipantNotConfiguredCode, row.FailureReason);
        Assert.Equal(0, handler.Calls);
        Assert.Null(row.ProviderMessageId);
        Assert.NotEqual(EinvoiceSubmissionStatuses.Ack, row.Status);
    }

    [Fact]
    public async Task OtherTenant_DoesNotCallAccessPoint()
    {
        var harness = await HarnessAsync(new Script(ProviderMessageId, DeliverAfterPolls: 1, Error: false), canary: Guid.NewGuid());

        await harness.Submission.SubmitAsync(harness.TenantId, Document(), invoiceId: harness.InvoiceId);

        var row = await ReloadAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Queued, row.Status);
        Assert.Equal(PeppolSubmissionService.ReservedFailureReason, row.FailureReason);
        harness.AccessPoint.Verify(
            client => client.SubmitAsync(It.IsAny<PeppolSubmitRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.AccessPoint.Verify(
            client => client.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnvironmentOtherThanTest_DoesNotCallAccessPoint()
    {
        var harness = await HarnessAsync(
            new Script(ProviderMessageId, DeliverAfterPolls: 1, Error: false),
            environment: "LIVE");

        await harness.Submission.SubmitAsync(harness.TenantId, Document(), invoiceId: harness.InvoiceId);

        var row = await ReloadAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Failed, row.Status);
        Assert.Equal(PeppolSubmissionService.LiveNotAllowedReason, row.FailureReason);
        Assert.Null(row.AttemptedAtUtc);
        harness.AccessPoint.Verify(
            client => client.SubmitAsync(It.IsAny<PeppolSubmitRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async Task<Harness> HarnessAsync(Script script, Guid? canary = null, string environment = "TEST")
    {
        var tenantId = Guid.NewGuid();
        var dbName = $"PeppolE2E_{Guid.NewGuid():N}";
        var invoiceId = await SeedAsync(tenantId, dbName, legalEntityId: "le-canary");
        var accessPoint = MockAccessPoint(script);
        var options = Options.Create(OptionsFor(canary ?? tenantId, environment));
        var factory = new NamedDbFactory(dbName, tenantId);
        var audit = AuditThatWrites(factory);
        var db = CreateDb(tenantId, dbName);
        var submission = Submission(db, options, accessPoint.Object, storecove: null, audit);
        var poller = Poller(factory, options, accessPoint.Object, audit.Object);
        return new Harness(db, submission, poller, accessPoint, tenantId, invoiceId);
    }

    private static PeppolSubmissionService Submission(
        AppDbContext db,
        IOptions<PeppolOptions> options,
        IPeppolAccessPointClient? canaryTransport,
        StorecovePeppolAccessPointClient? storecove,
        Mock<IAuditLogService> audit)
    {
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(flag => flag.IsEnabled(FeatureFlagNames.EInvoicingEn16931, It.IsAny<string?>())).Returns(true);
        return new PeppolSubmissionService(
            new En16931UblXmlBuilder(flags.Object),
            options,
            new MockPeppolAccessPointClient(),
            new HostedPeppolAccessPointClient(options, new HttpClient(new RecordingHandler(_ => throw new InvalidOperationException("socket")))),
            new InMemoryPeppolSubmissionStore(),
            audit: audit.Object,
            featureFlags: flags.Object,
            db: db,
            storecove: storecove,
            canaryTransport: canaryTransport);
    }

    private static PeppolAckPollingService Poller(
        IDbContextFactory<AppDbContext> factory,
        IOptions<PeppolOptions> options,
        IPeppolAccessPointClient accessPoint,
        IAuditLogService audit)
    {
        var services = new ServiceCollection();
        services.AddSingleton(factory);
        services.AddSingleton(accessPoint);
        services.AddSingleton(audit);
        var provider = services.BuildServiceProvider();
        return new PeppolAckPollingService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedMonitor(options.Value),
            NullLogger<PeppolAckPollingService>.Instance);
    }

    private static Mock<IPeppolAccessPointClient> MockAccessPoint(Script script)
    {
        var polls = 0;
        var accessPoint = new Mock<IPeppolAccessPointClient>();
        accessPoint
            .Setup(client => client.SubmitAsync(It.IsAny<PeppolSubmitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeppolTransportResult(PeppolSubmissionStatus.Sent, "processing", script.Guid));
        accessPoint
            .Setup(client => client.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string submissionId, CancellationToken _) =>
            {
                polls++;
                if (script.Error)
                {
                    return new PeppolTransportResult(
                        PeppolSubmissionStatus.Sent,
                        PeppolAckPollingService.ErrorState,
                        script.Guid);
                }

                var state = polls >= script.DeliverAfterPolls
                    ? PeppolAckPollingService.DeliveredState
                    : "processing";
                return new PeppolTransportResult(PeppolSubmissionStatus.Sent, state, submissionId == script.Guid ? script.Guid : submissionId);
            });
        return accessPoint;
    }

    private static Mock<IAuditLogService> AuditThatWrites(IDbContextFactory<AppDbContext> factory)
    {
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
            .Returns(new InvocationFunc(invocation => WriteAuditAsync(factory, invocation)));
        return audit;
    }

    private static async Task<AuditLog> WriteAuditAsync(IDbContextFactory<AppDbContext> factory, IInvocation invocation)
    {
        await using var db = await factory.CreateDbContextAsync();
        var row = new AuditLog
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid().ToString("N"),
            UserId = invocation.Arguments[2] as string ?? "system",
            UserRole = invocation.Arguments[3] as string ?? "SuperAdmin",
            Action = invocation.Arguments[0] as string ?? "EINVOICE",
            EntityType = invocation.Arguments[1] as string ?? "EinvoiceSubmission",
            Description = invocation.Arguments[4] as string,
            Status = invocation.Arguments[6] is AuditLogStatus status ? status : AuditLogStatus.Success,
            Timestamp = DateTime.UtcNow,
            ActionType = invocation.Arguments[12] as AuditEventType?,
            EntityId = invocation.Arguments[13] as Guid?,
            TenantId = invocation.Arguments[14] as Guid? ?? Guid.Empty,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.AuditLogs.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static async Task<Guid> SeedAsync(Guid tenantId, string dbName, string? legalEntityId)
    {
        var invoiceId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        await using var db = CreateDb(tenantId, dbName);
        var now = DateTime.UtcNow;
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "de-canary", Slug = "de-canary", IsActive = true });
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = tenantId,
            CompanyName = "Canary GmbH",
            CompanyAddress = "Berlin",
            CompanyTaxNumber = "DE123456789",
            Country = "DE",
            VatRegime = VatRegime.DE_USTG_STANDARD,
            BusinessHours = new Dictionary<string, string>(),
            Currency = "EUR",
            Language = "de-DE",
            TimeZone = "Europe/Berlin",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm:ss",
            TaxCalculationMethod = "Standard",
            InvoiceNumbering = "Sequential",
            ReceiptNumbering = "Sequential",
            DefaultPaymentMethod = "Cash",
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "1",
            IsActive = true,
            Status = RegisterStatus.Open,
            CreatedAt = now,
        });
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            InvoiceNumber = "DE-2026-22",
            InvoiceDate = now,
            DueDate = now.AddDays(14),
            Status = InvoiceStatus.Paid,
            Subtotal = 100m,
            TaxAmount = 19m,
            TotalAmount = 119m,
            PaidAmount = 119m,
            RemainingAmount = 0,
            CompanyName = "Canary GmbH",
            CompanyTaxNumber = "DE123456789",
            CompanyAddress = "Berlin",
            TseSignature = "sig",
            KassenId = "1",
            TseTimestamp = now,
            CashRegisterId = registerId,
            CreatedAt = now,
            IsActive = true,
        });
        db.PeppolParticipants.Add(new PeppolParticipant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ParticipantId = "iso6523-actorid-upis::9930:DE123456789",
            ApEnvironment = PeppolApEnvironments.Test,
            LegalEntityId = legalEntityId,
            EIdentifierScheme = "iso6523-actorid-upis",
            EIdentifierValue = "DE123456789",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await db.SaveChangesAsync();
        return invoiceId;
    }

    private static PeppolOptions OptionsFor(Guid canary, string environment) => new()
    {
        AccessPointMode = "hosted",
        Provider = "storecove",
        Environment = "TEST",
        AckPollInterval = 5,
        ReservedExit = new PeppolReservedExitOptions
        {
            Enabled = true,
            CanaryTenantId = canary.ToString("D"),
        },
        Storecove = new PeppolStorecoveOptions
        {
            ApiKey = "canary-test-key",
            BaseUrl = "https://api.storecove.test/api/v2/",
            Environment = environment,
        },
    };

    private static async Task AgeAsync(AppDbContext db, TimeSpan age)
    {
        var row = await db.EinvoiceSubmissions.IgnoreQueryFilters().SingleAsync();
        var stamp = DateTime.UtcNow - age;
        row.AttemptedAtUtc = stamp;
        row.CreatedAtUtc = stamp;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static async Task<EinvoiceSubmission> ReloadAsync(AppDbContext db)
    {
        db.ChangeTracker.Clear();
        return await db.EinvoiceSubmissions.IgnoreQueryFilters().SingleAsync();
    }

    private static async Task<string> CountryAsync(AppDbContext db) =>
        await db.CompanySettings.IgnoreQueryFilters().Select(row => row.Country).SingleAsync();

    private static AppDbContext CreateDb(Guid tenantId, string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static InvoiceDocumentDto Document() => new()
    {
        CountryCode = "EU_DEFAULT",
        InvoiceNumber = "DE-2026-22",
        InvoiceDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        Currency = "EUR",
        SellerName = "Canary GmbH",
        SellerVatId = "DE123456789",
        SellerCountry = "DE",
        BuyerName = "Buyer BV",
        BuyerVatId = "NL123456789B01",
        BuyerCountry = "NL",
        NetAmount = 100m,
        TaxAmount = 19m,
        GrossAmount = 119m,
        VatCategory = "S",
        VatPercent = 19m,
    };

    private sealed record Script(string Guid, int DeliverAfterPolls, bool Error);

    private sealed record Harness(
        AppDbContext Db,
        PeppolSubmissionService Submission,
        PeppolAckPollingService Poller,
        Mock<IPeppolAccessPointClient> AccessPoint,
        Guid TenantId,
        Guid InvoiceId);

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
        private readonly Guid _tenantId;

        public NamedDbFactory(string name, Guid tenantId)
        {
            _name = name;
            _tenantId = tenantId;
        }

        public AppDbContext CreateDbContext() => CreateDb(_tenantId, _name);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDb(_tenantId, _name));
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
