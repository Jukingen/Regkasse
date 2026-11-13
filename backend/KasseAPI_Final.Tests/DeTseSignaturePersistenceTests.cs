using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.KassenSicherheit;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DeTseSignaturePersistenceTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();

    [Fact]
    public async Task SignDe_WritesDeTable_AndLeavesAtColumnsNull()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.PaymentDetails.Add(Payment(paymentId, registerId));
        await db.SaveChangesAsync();

        var kassen = new Mock<IKassenSicherheitService>();
        kassen.Setup(x => x.SignAsync(It.IsAny<KassenSicherheitSignRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KassenSicherheitSignResult(true, "sig-de", "fiskaly-de"));
        var tse = new Mock<ITseService>(MockBehavior.Strict);
        var router = Router(db, kassen.Object, tse.Object, deEnabled: true);

        var result = await router.SignAsync(Context(tenantId, registerId, paymentId));
        await db.SaveChangesAsync();

        var row = await db.DeTseSignatures.SingleAsync();
        var payment = await db.PaymentDetails.SingleAsync();
        Assert.Equal("sig-de", result.Signature);
        Assert.Equal("sig-de", row.Signature);
        Assert.Equal(paymentId, row.PaymentDetailsId);
        Assert.Equal("tss-1", row.TssId);
        Assert.Null(payment.TseSignature == string.Empty ? null : payment.TseSignature);
        Assert.True(string.IsNullOrEmpty(payment.TseSignature));
        Assert.True(string.IsNullOrEmpty(payment.PrevSignatureValueUsed));
        Assert.True(string.IsNullOrEmpty(payment.CertificateThumbprint));
        tse.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SignAt_DoesNotWriteDeSignatureRow()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        await using var db = CreateDb(tenantId);
        db.PaymentDetails.Add(Payment(paymentId, registerId, tseSignature: "header.payload.sig"));
        await db.SaveChangesAsync();

        var tse = new Mock<ITseService>();
        tse.Setup(x => x.CreateInvoiceSignatureAsync(
                registerId, "AT-1", 120m, "1", "prev", null, "{}", null))
            .ReturnsAsync(new TseSignatureResult("header.payload.sig", "prev-used", "thumb"));
        var kassen = new Mock<IKassenSicherheitService>(MockBehavior.Strict);
        var router = Router(db, kassen.Object, tse.Object, deEnabled: false);

        await router.SignAsync(new FiscalSignatureContext(
            Binding(tenantId, CountryProfileCodes.Austria, VatRegime.AT_RKSV_STANDARD),
            [TaxLineItemInput.FromVatPercent(120m, 1, 20m)],
            120m,
            "AT-1",
            CashRegisterId: registerId,
            RegisterNumber: "1",
            PrevSignatureValue: "prev",
            TaxDetailsJson: "{}",
            PaymentId: paymentId));
        await db.SaveChangesAsync();

        Assert.Empty(await db.DeTseSignatures.ToListAsync());
        var payment = await db.PaymentDetails.SingleAsync();
        Assert.Equal("header.payload.sig", payment.TseSignature);
    }

    [Fact]
    public async Task SignDe_HttpResponse_PersistsAlgorithmAndCertificateSerial()
    {
        using var cert = CreateCert();
        var expectedSerial = cert.SerialNumber;
        var certB64 = Convert.ToBase64String(cert.Export(X509ContentType.Cert));
        var logs = new ListLogger();
        var harness = await HttpRouterAsync(
            logs,
            txJson: """{"signature":{"value":"sig-de","algorithm":"ecdsa-plain-SHA256"}}""",
            tssJson: "{\"state\":\"INITIALIZED\",\"certificate\":\"" + certB64 + "\"}");

        await using (harness.Db)
        {
            await harness.Router.SignAsync(Context(harness.TenantId, harness.RegisterId, harness.PaymentId));
            await harness.Db.SaveChangesAsync();
            var row = await harness.Db.DeTseSignatures.SingleAsync();
            Assert.Equal("ecdsa-plain-SHA256", row.SignatureAlgorithm);
            Assert.Equal(expectedSerial, row.CertificateSerial);
            Assert.True(string.IsNullOrEmpty((await harness.Db.PaymentDetails.SingleAsync()).TseSignature));
        }

        Assert.DoesNotContain(logs.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task SignDe_HttpResponseWithoutFields_PersistsNull_AndLogsWarning()
    {
        var logs = new ListLogger();
        var harness = await HttpRouterAsync(
            logs,
            txJson: """{"signature":{"value":"sig-de"}}""",
            tssJson: """{"state":"INITIALIZED"}""");

        await using (harness.Db)
        {
            await harness.Router.SignAsync(Context(harness.TenantId, harness.RegisterId, harness.PaymentId));
            await harness.Db.SaveChangesAsync();
            var row = await harness.Db.DeTseSignatures.SingleAsync();
            Assert.Equal("sig-de", row.Signature);
            Assert.Null(row.SignatureAlgorithm);
            Assert.Null(row.CertificateSerial);
        }

        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("signature.algorithm", StringComparison.Ordinal));
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("certificate serial", StringComparison.Ordinal));
    }

    private static FiscalSignatureRouter Router(
        AppDbContext db,
        IKassenSicherheitService kassen,
        ITseService tse,
        bool deEnabled)
    {
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, It.IsAny<string?>()))
            .Returns(deEnabled);
        return new FiscalSignatureRouter(
            flags.Object,
            tse: tse,
            kassen: kassen,
            db: db,
            kassenOptions: Options.Create(new KassenSicherheitOptions { Provider = "fiskaly-de" }));
    }

    private static FiscalSignatureContext Context(Guid tenantId, Guid registerId, Guid paymentId) =>
        new(
            Binding(tenantId, CountryProfileCodes.Germany, VatRegime.DE_USTG_STANDARD),
            [TaxLineItemInput.FromVatPercent(119m, 1, 19m)],
            119m,
            "DE-dev-1-1",
            CashRegisterId: registerId,
            RegisterNumber: "1",
            TaxDetailsJson: "{}",
            PaymentId: paymentId);

    private static CountryStrategyBinding Binding(Guid tenantId, string country, VatRegime regime) =>
        new(
            new CompanySettings
            {
                TenantId = tenantId,
                Country = country,
                VatRegime = regime,
                CompanyName = "Mandant",
                Currency = "EUR",
                DeTssId = "tss-1",
                DeClientId = "client-1",
            },
            Profiles.Get(country),
            regime,
            UsedLegacyFallback: false);

    private static PaymentDetails Payment(Guid id, Guid registerId, string? tseSignature = null) =>
        new()
        {
            Id = id,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Gast",
            CashierId = "c1",
            TotalAmount = 1m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            ReceiptNumber = "DE-dev-1-1",
            TseSignature = tseSignature ?? string.Empty,
            PaymentItems = System.Text.Json.JsonDocument.Parse("[]"),
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
        };

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DeTse_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static async Task<HttpHarness> HttpRouterAsync(ListLogger logs, string txJson, string tssJson)
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var db = CreateDb(tenantId);
        db.PaymentDetails.Add(Payment(paymentId, registerId));
        await db.SaveChangesAsync();

        var handler = new JsonHandler(txJson, tssJson);
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var options = Microsoft.Extensions.Options.Options.Create(new KassenSicherheitOptions
        {
            Provider = "fiskaly-de",
            Environment = "TEST",
            ApiBaseUrl = "https://kassensichv-middleware.fiskaly.com/api/v2",
            ApiKey = "key-1",
            ApiSecret = "secret-value",
            AdminPin = "pin-value",
        });
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, It.IsAny<string?>())).Returns(true);
        var client = new FiskalyDeKassenSicherheitHttpClient(http, options, logs);
        var service = new FiskalyDeKassenSicherheitService(flags.Object, options, client);
        var router = new FiscalSignatureRouter(
            flags.Object,
            kassen: service,
            db: db,
            kassenOptions: options);
        return new HttpHarness(db, router, tenantId, registerId, paymentId);
    }

    private static X509Certificate2 CreateCert()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=tss-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
    }

    private sealed record HttpHarness(
        AppDbContext Db,
        FiscalSignatureRouter Router,
        Guid TenantId,
        Guid RegisterId,
        Guid PaymentId);

    private sealed class JsonHandler : HttpMessageHandler
    {
        private readonly string _txJson;
        private readonly string _tssJson;

        public JsonHandler(string txJson, string tssJson)
        {
            _txJson = txJson;
            _tssJson = tssJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body = path.EndsWith("/auth", StringComparison.Ordinal)
                ? """{"access_token":"tok-1","access_token_expires_in":120}"""
                : path.Contains("/tx/", StringComparison.Ordinal)
                    ? _txJson
                    : _tssJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class ListLogger : ILogger<FiskalyDeKassenSicherheitHttpClient>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
