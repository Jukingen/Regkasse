using System.Net;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class StorecovePeppolAccessPointClientTests
{
    private const string BaseUrl = "https://api.storecove.com/api/v2/";
    private static readonly Guid SubmissionId = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
    private static readonly Guid TenantId = Guid.Parse("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

    [Fact]
    public async Task Submit_PostsDocumentSubmission_AndMapsGuidToSent()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """{"guid":"sc-msg-1"}"""));
        var client = Client(handler);

        var result = await client.SubmitAsync(new PeppolSubmitRequest(
            SubmissionId.ToString("D"),
            "<Invoice>ubl</Invoice>",
            "9915:DE123",
            TenantId));

        Assert.Equal(PeppolSubmissionStatus.Sent, result.Status);
        Assert.NotEqual(PeppolSubmissionStatus.Ack, result.Status);
        Assert.Equal("sc-msg-1", result.ProviderMessageId);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(BaseUrl + "document_submissions", request.Url);
        Assert.Equal("Bearer", request.Scheme);
        Assert.Equal("test-key", request.Parameter);
        Assert.Contains("idempotencyGuid", request.Body, StringComparison.Ordinal);
        Assert.Contains(SubmissionId.ToString("D"), request.Body, StringComparison.Ordinal);
        Assert.Contains("legalEntityId", request.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("<Invoice>ubl</Invoice>", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_WithParticipantIdentity_SendsLegalEntitySchemeAndId()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """{"guid":"sc-msg-1"}"""));
        var client = Client(handler);

        await client.SubmitAsync(Request());

        var body = Assert.Single(handler.Requests).Body;
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("le-42", doc.RootElement.GetProperty("legalEntityId").GetString());
        var identifier = doc.RootElement.GetProperty("routing").GetProperty("eIdentifiers").EnumerateArray().Single();
        Assert.Equal("iso6523-actorid-upis", identifier.GetProperty("scheme").GetString());
        Assert.Equal("DE123456789", identifier.GetProperty("id").GetString());
        Assert.Equal(2, identifier.EnumerateObject().Count());
    }

    [Fact]
    public async Task Submit_WithoutLegalEntityId_DoesNotOpenHttp()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("socket"));
        var client = Client(handler, legalEntityId: null);

        var ex = await Assert.ThrowsAsync<PeppolTransportException>(() => client.SubmitAsync(Request()));

        Assert.Equal(StorecovePeppolAccessPointClient.ParticipantNotConfiguredCode, ex.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Submit_WithoutScheme_DoesNotOpenHttp()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("socket"));
        var client = Client(handler, scheme: null);

        var ex = await Assert.ThrowsAsync<PeppolTransportException>(() => client.SubmitAsync(Request()));

        Assert.Equal(StorecovePeppolAccessPointClient.ParticipantNotConfiguredCode, ex.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetStatus_DoesNotTreatHttpSuccessAsAck()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """{"guid":"sc-msg-1","status":"processing"}"""));
        var client = Client(handler);

        var result = await client.GetStatusAsync("sc-msg-1");

        Assert.Equal(PeppolSubmissionStatus.Sent, result.Status);
        Assert.Equal("processing", result.Detail);
        Assert.Equal("sc-msg-1", result.ProviderMessageId);
        Assert.Equal(HttpMethod.Get, Assert.Single(handler.Requests).Method);
        Assert.EndsWith("/document_submissions/sc-msg-1", Assert.Single(handler.Requests).Url, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task ErrorStatus_ThrowsPeppolTransportException(HttpStatusCode status)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent("<Invoice>secret</Invoice>", Encoding.UTF8, "application/xml"),
        });
        var client = Client(handler);

        var ex = await Assert.ThrowsAsync<PeppolTransportException>(() =>
            client.SubmitAsync(Request()));

        Assert.Equal((int)status, ex.StatusCode);
        Assert.DoesNotContain("secret", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("test-key", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Timeout_ThrowsPeppolTransportException_NotTaskCanceled()
    {
        var handler = new RecordingHandler(async (request, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            return Json(HttpStatusCode.OK, """{"guid":"late"}""");
        });
        var client = Client(handler, timeout: TimeSpan.FromMilliseconds(30));

        var ex = await Assert.ThrowsAsync<PeppolTransportException>(() =>
            client.SubmitAsync(Request()));

        Assert.IsNotType<TaskCanceledException>(ex);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public async Task LiveEnvironment_DoesNotOpenHttp()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("socket"));
        var client = Client(handler, environment: "LIVE");

        var ex = await Assert.ThrowsAsync<PeppolTransportException>(() => client.SubmitAsync(Request()));

        Assert.Empty(handler.Requests);
        Assert.Equal(StorecovePeppolAccessPointClient.LiveNotAllowedCode, ex.Code);
    }

    [Fact]
    public async Task TestEnvironment_OpensHttp()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, """{"guid":"sc-msg-1"}"""));
        var client = Client(handler, environment: "TEST");

        await client.SubmitAsync(Request());

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ProviderNotConfigured_SelectsHostedClient_AndOpensNoHttp()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("socket"));
        var options = Options.Create(new PeppolOptions
        {
            Provider = "not-configured",
            AccessPointMode = "hosted",
            BaseUrl = BaseUrl,
        });
        var hosted = new HostedPeppolAccessPointClient(options, new HttpClient(handler));
        var storecove = Client(new RecordingHandler(_ => throw new InvalidOperationException("storecove")));

        var selected = PeppolAccessPointClientFactory.Select("not-configured", hosted, storecove);

        Assert.Same(hosted, selected);
        await Assert.ThrowsAsync<PeppolAccessPointNotConfiguredException>(() =>
            selected.SubmitAsync(Request()));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SubmitAsync_WhileReserved_DoesNotCallStorecove()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("socket"));
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(flag => flag.IsEnabled(FeatureFlagNames.EInvoicingEn16931, It.IsAny<string?>())).Returns(true);
        var options = Options.Create(new PeppolOptions
        {
            Provider = "storecove",
            AccessPointMode = "hosted",
            Storecove = new PeppolStorecoveOptions
            {
                BaseUrl = BaseUrl,
                ApiKey = "test-key",
                Environment = "TEST",
            },
        });
        var service = new PeppolSubmissionService(
            new En16931UblXmlBuilder(flags.Object),
            options,
            new MockPeppolAccessPointClient(),
            new HostedPeppolAccessPointClient(options, new HttpClient(handler)),
            new InMemoryPeppolSubmissionStore(),
            featureFlags: flags.Object);

        var row = await service.SubmitAsync(Guid.NewGuid(), Document(), "9915:DE123");

        Assert.Equal(PeppolSubmissionStatus.Queued, row.Status);
        Assert.Equal(PeppolSubmissionService.ReservedFailureReason, row.Detail);
        Assert.Empty(handler.Requests);
        Assert.Contains(FeatureFlagNames.EInvoicingPeppol, FeatureFlagNames.Reserved);
        Assert.DoesNotContain(FeatureFlagNames.EInvoicingPeppol, FeatureFlagNames.All);
    }

    private static StorecovePeppolAccessPointClient Client(
        HttpMessageHandler handler,
        string environment = "TEST",
        TimeSpan? timeout = null,
        string? legalEntityId = "le-42",
        string? scheme = "iso6523-actorid-upis",
        string? value = "DE123456789")
    {
        var http = new HttpClient(handler);
        if (timeout is TimeSpan limit)
            http.Timeout = limit;
        var dbName = $"Storecove_{Guid.NewGuid():N}";
        using (var db = CreateDb(dbName))
        {
            var now = DateTime.UtcNow;
            db.PeppolParticipants.Add(new PeppolParticipant
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ParticipantId = "9915:DE123",
                ApEnvironment = PeppolApEnvironments.Test,
                LegalEntityId = legalEntityId,
                EIdentifierScheme = scheme,
                EIdentifierValue = value,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
            db.SaveChanges();
        }

        return new StorecovePeppolAccessPointClient(
            Options.Create(new PeppolOptions
            {
                Provider = "storecove",
                Storecove = new PeppolStorecoveOptions
                {
                    ApiKey = "test-key",
                    BaseUrl = BaseUrl,
                    Environment = environment,
                },
            }),
            http,
            new NamedDbFactory(dbName));
    }

    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static PeppolSubmitRequest Request() =>
        new(SubmissionId.ToString("D"), "<Invoice/>", "9915:DE123", TenantId);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

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

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
            : this((request, _) => Task.FromResult(send(request)))
        {
        }

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        public List<Captured> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new Captured(
                request.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                body));
            return await _send(request, cancellationToken);
        }
    }

    private sealed record Captured(HttpMethod Method, string Url, string? Scheme, string? Parameter, string Body);

    private sealed class NamedDbFactory : IDbContextFactory<AppDbContext>
    {
        private readonly string _name;

        public NamedDbFactory(string name) => _name = name;

        public AppDbContext CreateDbContext() => CreateDb(_name);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDb(_name));
    }
}
