using System.Net;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PeppolSubmissionTests
{
    [Fact]
    public async Task NotConfigured_ValidatesAndDoesNotSend()
    {
        var service = Service("not-configured");
        var row = await service.SubmitAsync(Guid.NewGuid(), Fixture(100m, 20m));

        Assert.Equal(PeppolSubmissionStatus.Validated, row.Status);
        Assert.Equal("not-sent", row.Detail);
    }

    [Fact]
    public async Task Mock_SubmitThenPoll_ReachesAck()
    {
        var service = Service("mock");
        var sent = await service.SubmitAsync(Guid.NewGuid(), Fixture(100m, 20m), "iso6523-actorid-upis::9915:DE123456789");
        Assert.Equal(PeppolSubmissionStatus.Sent, sent.Status);

        var ack = await service.GetStatusAsync(sent.Id);
        Assert.NotNull(ack);
        Assert.Equal(PeppolSubmissionStatus.Ack, ack!.Status);
    }

    [Fact]
    public async Task SchematronFailure_DoesNotSend()
    {
        var service = Service("mock");
        var row = await service.SubmitAsync(Guid.NewGuid(), Fixture(100m, 50m));

        Assert.Equal(PeppolSubmissionStatus.Failed, row.Status);
        Assert.Contains("BR-S-01", row.Detail, StringComparison.Ordinal);
        var stored = await service.GetStatusAsync(row.Id);
        Assert.Equal(PeppolSubmissionStatus.Failed, stored!.Status);
    }

    [Fact]
    public async Task Hosted_WithoutBaseUrl_FailsClosed()
    {
        var service = Service("hosted", baseUrl: "");
        var row = await service.SubmitAsync(Guid.NewGuid(), Fixture(100m, 20m));

        Assert.Equal(PeppolSubmissionStatus.Failed, row.Status);
        Assert.Contains("not configured", row.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hosted_HttpClient_SubmitThenStatus()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler);
        var options = Options.Create(new PeppolOptions
        {
            AccessPointMode = "hosted",
            Provider = "hosted",
            Environment = "TEST",
            BaseUrl = "https://ap.test.example/v1",
        });
        var client = new HostedPeppolAccessPointClient(options, http);

        var sent = await client.SubmitAsync(new PeppolSubmitRequest("abc", "<Invoice/>", "9915:DE123456789"));
        var ack = await client.GetStatusAsync("abc");

        Assert.Equal(PeppolSubmissionStatus.Sent, sent.Status);
        Assert.Equal(PeppolSubmissionStatus.Ack, ack.Status);
        Assert.Equal(HttpMethod.Post, handler.Methods[0]);
        Assert.Equal(HttpMethod.Get, handler.Methods[1]);
        Assert.Equal("https://ap.test.example/v1/submissions", handler.Urls[0]);
    }

    private static PeppolSubmissionService Service(string provider, string baseUrl = "")
    {
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabled(FeatureFlagNames.EInvoicingEn16931, It.IsAny<string?>())).Returns(true);
        return new PeppolSubmissionService(
            new En16931UblXmlBuilder(flags.Object),
            Options.Create(new PeppolOptions
            {
                AccessPointMode = "hosted",
                Provider = provider,
                BaseUrl = baseUrl,
            }),
            new MockPeppolAccessPointClient(),
            new HostedPeppolAccessPointClient(
                Options.Create(new PeppolOptions
                {
                    AccessPointMode = "hosted",
                    Provider = provider,
                    BaseUrl = baseUrl,
                }),
                new HttpClient(new StubHandler())),
            new InMemoryPeppolSubmissionStore());
    }

    private static InvoiceDocumentDto Fixture(decimal net, decimal tax) => new()
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
        NetAmount = net,
        TaxAmount = tax,
        GrossAmount = net + tax,
        VatCategory = "S",
        VatPercent = 20m,
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpMethod> Methods { get; } = [];
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
