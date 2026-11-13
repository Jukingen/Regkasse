using System.Net;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PeppolCanarySubmissionTests
{
    private const string ApiKey = "canary-test-key";
    private const string UblMarker = "secret-ubl-marker";

    [Fact]
    public async Task ReservedExitDisabled_DoesNotCallHttp_AndStaysQueued()
    {
        var tenantId = Guid.NewGuid();
        var harness = await HarnessAsync(tenantId, exitEnabled: false, canary: tenantId, environment: "TEST");

        var row = await harness.Service.SubmitAsync(tenantId, Document(), invoiceId: harness.InvoiceId);

        Assert.Equal(0, harness.Handler.Calls);
        Assert.Equal(PeppolSubmissionStatus.Queued, row.Status);
        var stored = await StoredAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Queued, stored.Status);
        Assert.Equal(PeppolSubmissionService.ReservedFailureReason, stored.FailureReason);
        Assert.Null(stored.AttemptedAtUtc);
        Assert.Null(stored.AckedAtUtc);
        Assert.NotEqual(EinvoiceSubmissionStatuses.Ack, stored.Status);
    }

    [Fact]
    public async Task Enabled_NonCanaryTenant_DoesNotCallHttp_AndStaysQueued()
    {
        var tenantId = Guid.NewGuid();
        var harness = await HarnessAsync(tenantId, exitEnabled: true, canary: Guid.NewGuid(), environment: "TEST");

        var row = await harness.Service.SubmitAsync(tenantId, Document(), invoiceId: harness.InvoiceId);

        Assert.Equal(0, harness.Handler.Calls);
        Assert.Equal(PeppolSubmissionStatus.Queued, row.Status);
        var stored = await StoredAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Queued, stored.Status);
        Assert.Equal(PeppolSubmissionService.ReservedFailureReason, stored.FailureReason);
    }

    [Fact]
    public async Task Canary_TestStorecove_Http2xx_WritesSent_AndProviderMessageId()
    {
        var tenantId = Guid.NewGuid();
        var harness = await HarnessAsync(
            tenantId,
            exitEnabled: true,
            canary: tenantId,
            environment: "TEST",
            response: _ => Json(HttpStatusCode.OK, """{"guid":"sc-guid-9","status":"processing"}"""));

        var row = await harness.Service.SubmitAsync(
            tenantId,
            Document(),
            "iso6523-actorid-upis::9915:DE123",
            invoiceId: harness.InvoiceId);

        Assert.Equal(1, harness.Handler.Calls);
        Assert.Equal(PeppolSubmissionStatus.Sent, row.Status);
        Assert.NotEqual(PeppolSubmissionStatus.Ack, row.Status);
        Assert.Contains("idempotencyGuid", harness.Handler.Body, StringComparison.Ordinal);
        var stored = await StoredAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Sent, stored.Status);
        Assert.Equal("sc-guid-9", stored.ProviderMessageId);
        Assert.Equal("processing", stored.ProviderStatus);
        Assert.NotNull(stored.AttemptedAtUtc);
        Assert.Null(stored.FailureReason);
        Assert.Null(stored.AckedAtUtc);
        Assert.NotEqual(EinvoiceSubmissionStatuses.Ack, stored.Status);
        Assert.DoesNotContain(UblMarker, harness.Logger.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, harness.Logger.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Canary_Storecove4xx_WritesFailed_WithoutUblInTheLog()
    {
        var tenantId = Guid.NewGuid();
        var harness = await HarnessAsync(
            tenantId,
            exitEnabled: true,
            canary: tenantId,
            environment: "TEST",
            response: _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    $"<Invoice>{UblMarker}</Invoice>{ApiKey}",
                    Encoding.UTF8,
                    "application/xml"),
            });

        var row = await harness.Service.SubmitAsync(tenantId, Document(), invoiceId: harness.InvoiceId);

        Assert.Equal(1, harness.Handler.Calls);
        Assert.Equal(PeppolSubmissionStatus.Failed, row.Status);
        Assert.NotEqual(PeppolSubmissionStatus.Ack, row.Status);
        var stored = await StoredAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Failed, stored.Status);
        Assert.Equal("storecove-http-400", stored.FailureReason);
        Assert.NotNull(stored.AttemptedAtUtc);
        Assert.Null(stored.AckedAtUtc);
        Assert.Null(stored.ProviderMessageId);
        Assert.DoesNotContain(UblMarker, stored.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, stored.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain(UblMarker, harness.Logger.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, harness.Logger.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<Invoice", harness.Logger.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Canary_LiveEnvironment_WritesFailed_AndDoesNotCallHttp()
    {
        var tenantId = Guid.NewGuid();
        var harness = await HarnessAsync(tenantId, exitEnabled: true, canary: tenantId, environment: "LIVE");

        var row = await harness.Service.SubmitAsync(tenantId, Document(), invoiceId: harness.InvoiceId);

        Assert.Equal(0, harness.Handler.Calls);
        Assert.Equal(PeppolSubmissionStatus.Failed, row.Status);
        var stored = await StoredAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Failed, stored.Status);
        Assert.Equal(PeppolSubmissionService.LiveNotAllowedReason, stored.FailureReason);
        Assert.Null(stored.AttemptedAtUtc);
        Assert.NotEqual(EinvoiceSubmissionStatuses.Ack, stored.Status);
    }

    [Fact]
    public async Task Canary_ProviderNotConfigured_WritesFailed_AndDoesNotCallHttp()
    {
        var tenantId = Guid.NewGuid();
        var harness = await HarnessAsync(
            tenantId,
            exitEnabled: true,
            canary: tenantId,
            environment: "TEST",
            provider: "not-configured");

        var row = await harness.Service.SubmitAsync(tenantId, Document(), invoiceId: harness.InvoiceId);

        Assert.Equal(0, harness.Handler.Calls);
        Assert.Equal(PeppolSubmissionStatus.Failed, row.Status);
        var stored = await StoredAsync(harness.Db);
        Assert.Equal(EinvoiceSubmissionStatuses.Failed, stored.Status);
        Assert.Equal(PeppolSubmissionService.ProviderNotStorecoveReason, stored.FailureReason);
        Assert.False(string.IsNullOrWhiteSpace(stored.FailureReason));
    }

    private static async Task<Harness> HarnessAsync(
        Guid tenantId,
        bool exitEnabled,
        Guid canary,
        string environment,
        string provider = "storecove",
        Func<HttpRequestMessage, HttpResponseMessage>? response = null)
    {
        var invoiceId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var dbName = $"PeppolCanary_{Guid.NewGuid():N}";
        var db = CreateDb(tenantId, dbName);
        var now = DateTime.UtcNow;
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "eu", Slug = "eu", IsActive = true });
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
            InvoiceNumber = "EU-2026-83",
            InvoiceDate = now,
            DueDate = now.AddDays(14),
            Status = InvoiceStatus.Paid,
            Subtotal = 100m,
            TaxAmount = 20m,
            TotalAmount = 120m,
            PaidAmount = 120m,
            RemainingAmount = 0,
            CompanyName = "Seller GmbH",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Wien",
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
            ParticipantId = "iso6523-actorid-upis::9915:DE123",
            ApEnvironment = PeppolApEnvironments.Test,
            LegalEntityId = "le-canary",
            EIdentifierScheme = "iso6523-actorid-upis",
            EIdentifierValue = "DE123456789",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        var handler = new RecordingHandler(response ?? (_ => throw new InvalidOperationException("socket")));
        var http = new HttpClient(handler);
        var options = Options.Create(new PeppolOptions
        {
            AccessPointMode = "hosted",
            Provider = provider,
            Environment = "TEST",
            ApiKey = ApiKey,
            ReservedExit = new PeppolReservedExitOptions
            {
                Enabled = exitEnabled,
                CanaryTenantId = canary.ToString("D"),
            },
            Storecove = new PeppolStorecoveOptions
            {
                ApiKey = ApiKey,
                BaseUrl = "https://api.storecove.test/api/v2/",
                Environment = environment,
            },
        });
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(flag => flag.IsEnabled(FeatureFlagNames.EInvoicingEn16931, It.IsAny<string?>())).Returns(true);
        var logger = new CollectingLogger();
        var service = new PeppolSubmissionService(
            new En16931UblXmlBuilder(flags.Object),
            options,
            new MockPeppolAccessPointClient(),
            new HostedPeppolAccessPointClient(options, http),
            new InMemoryPeppolSubmissionStore(),
            featureFlags: flags.Object,
            db: db,
            storecove: new StorecovePeppolAccessPointClient(options, http, new NamedDbFactory(dbName, tenantId)),
            logger: logger);
        return new Harness(db, service, handler, logger, invoiceId);
    }

    private static async Task<EinvoiceSubmission> StoredAsync(AppDbContext db) =>
        await db.EinvoiceSubmissions.IgnoreQueryFilters().SingleAsync();

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
        InvoiceNumber = "EU-2026-83",
        InvoiceDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        Currency = "EUR",
        SellerName = "Seller GmbH",
        SellerVatId = "ATU12345678",
        SellerCountry = "AT",
        BuyerName = "Buyer BV",
        BuyerVatId = "DE123456789",
        BuyerCountry = "DE",
        NetAmount = 100m,
        TaxAmount = 20m,
        GrossAmount = 120m,
        VatCategory = "S",
        VatPercent = 20m,
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record Harness(
        AppDbContext Db,
        PeppolSubmissionService Service,
        RecordingHandler Handler,
        CollectingLogger Logger,
        Guid InvoiceId);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) => _response = response;

        public int Calls { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return _response(request);
        }
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

    private sealed class CollectingLogger : ILogger<PeppolSubmissionService>
    {
        public string Text => string.Join('\n', _lines);

        private readonly List<string> _lines = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _lines.Add(formatter(state, exception));
            if (exception is not null)
                _lines.Add(exception.ToString());
        }
    }
}
