using System.Net.Http;
using System.Reflection;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Services.Countries.KassenSicherheit;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// CI gate for Paket 20–22. Keeps DE signing, CH bank submit, and Peppol HTTP
/// off the default paths, and keeps <c>CompanySettings</c> on the single <c>country</c> column.
/// </summary>
public sealed class CountryLayerNewPackagesGateTests
{
    [Fact]
    public void PaymentService_DoesNotCallFiskalyDeKassenSicherheitServiceSignAsync()
    {
        var concrete = typeof(FiskalyDeKassenSicherheitService);
        var payment = typeof(PaymentService);

        Assert.DoesNotContain(
            payment.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            ctor => ctor.GetParameters().Any(parameter =>
                parameter.ParameterType == concrete || parameter.ParameterType == typeof(IKassenSicherheitService)));

        var source = File.ReadAllText(Path.Combine(FindBackendRoot(), "Services", "PaymentService.cs"));
        Assert.DoesNotContain("FiskalyDeKassenSicherheitService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IKassenSicherheitService", source, StringComparison.Ordinal);
    }

    [Fact]
    public void QrRechnung_MakesNoBankHttpCall_WhenBankSubmitEnabledIsFalse()
    {
        Assert.False(new QrRechnungOptions().BankSubmit.Enabled);
        Assert.False(new QrRechnungBankSubmitOptions().Enabled);

        // Option A (operator PDF download) calls BuildPdfAsync. That stays allowed.
        // Bank HTTP stays forbidden while QrRechnung:BankSubmit:Enabled is false.
        var markers = new[]
        {
            "HttpClient",
            "IHttpClientFactory",
            "HttpRequestMessage",
            "HttpMessageHandler",
            "SocketsHttpHandler",
            "WebRequest",
            "System.Net.Http",
        };

        var backendRoot = FindBackendRoot();
        var callers = Directory.EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains("KasseAPI_Final.Tests", StringComparison.Ordinal))
            .Where(path =>
                path.Contains("QrRechnung", StringComparison.OrdinalIgnoreCase)
                || path.Contains("ChQr", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .Where(file => markers.Any(marker => file.Text.Contains(marker, StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(backendRoot, file.Path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var hostPath = Path.Combine(backendRoot, "ApplicationHost.cs");
        if (File.Exists(hostPath))
        {
            foreach (var line in File.ReadAllLines(hostPath))
            {
                if (line.Contains("QrRechnung", StringComparison.Ordinal)
                    && markers.Any(marker => line.Contains(marker, StringComparison.Ordinal)))
                {
                    callers.Add("ApplicationHost.cs: " + line.Trim());
                }
            }
        }

        Assert.True(
            callers.Count == 0,
            "QrRechnung:BankSubmit:Enabled defaults to false. BuildPdfAsync stays allowed for "
            + "operator PDF download. No QrRechnung production type may open bank HTTP "
            + "(HttpClient, IHttpClientFactory, HttpRequestMessage, HttpMessageHandler) "
            + "while that switch is off. Call sites:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, callers));
    }

    [Fact]
    public async Task PeppolSubmissionService_MakesNoHttpCall_WhenEn16931FlagIsOff()
    {
        var handler = new CountingHandler();
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(flag => flag.IsEnabled(FeatureFlagNames.EInvoicingEn16931, It.IsAny<string?>())).Returns(false);
        var options = Options.Create(new PeppolOptions
        {
            AccessPointMode = "hosted",
            Provider = "hosted",
            BaseUrl = "https://ap.example/v1",
        });
        var store = new InMemoryPeppolSubmissionStore();
        var service = new PeppolSubmissionService(
            new En16931UblXmlBuilder(flags.Object),
            options,
            new MockPeppolAccessPointClient(),
            new HostedPeppolAccessPointClient(options, new HttpClient(handler)),
            store,
            featureFlags: flags.Object);

        var submitted = await service.SubmitAsync(Guid.NewGuid(), Fixture());
        store.Save(new PeppolSubmission(
            "sent-row",
            Guid.NewGuid(),
            "EU-1",
            PeppolSubmissionStatus.Sent,
            null,
            null));
        var polled = await service.GetStatusAsync("sent-row");

        Assert.Equal(PeppolSubmissionStatus.Validated, submitted.Status);
        Assert.Equal("not-sent", submitted.Detail);
        Assert.Equal(PeppolSubmissionStatus.Sent, polled!.Status);
        Assert.Equal(0, handler.Calls);
    }

    /// <summary>Same model check as <c>CompanySettingsCountryFieldsTests.Country_WasNotDuplicatedByASecondCountryCodeColumn</c>.</summary>
    [Fact]
    public void CompanySettings_HasNoCountryCodeColumn()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CountryLayerNewPackages_{Guid.NewGuid():N}")
            .Options;

        using var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        var company = db.Model.FindEntityType(typeof(CompanySettings));
        Assert.NotNull(company);

        var country = company!.FindProperty(nameof(CompanySettings.Country));
        Assert.NotNull(country);
        Assert.Equal("country", country!.GetColumnName(), ignoreCase: true);
        Assert.Null(company.FindProperty("CountryCode"));
        Assert.DoesNotContain(
            company.GetProperties(),
            property => string.Equals(property.GetColumnName(), "country_code", StringComparison.OrdinalIgnoreCase));

        var offenders = db.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties().Select(property => (Entity: entity.ClrType.Name, property)))
            .Where(pair =>
                pair.property.Name == "CountryCode"
                || string.Equals(pair.property.GetColumnName(), "country_code", StringComparison.OrdinalIgnoreCase))
            .Select(pair => $"{pair.Entity}.{pair.property.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "A CountryCode column is forbidden. Issue-time snapshots stay on CountryCodeAtIssue. Offenders:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    private static InvoiceDocumentDto Fixture() => new()
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

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KasseAPI_Final.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("Outbound HTTP is not allowed while EInvoicing.En16931 is off.");
        }
    }
}
