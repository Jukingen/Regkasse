using System.Net;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Two-way gate for <c>EInvoicing.Peppol</c>. Runtime checks prove the flag is reserved today.
/// The file scan fails if a later change opts the name into resolution or a country default.
/// </summary>
public sealed class PeppolReservedFlagGuardTests
{
    [Fact]
    public async Task SubmitAsync_DoesNotOpenHttp()
    {
        var handler = new CountingHandler();
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(flag => flag.IsEnabled(FeatureFlagNames.EInvoicingEn16931, It.IsAny<string?>())).Returns(true);
        var options = Options.Create(new PeppolOptions
        {
            AccessPointMode = "hosted",
            Provider = "hosted",
            BaseUrl = "https://ap.example/v1",
        });
        var service = new PeppolSubmissionService(
            new En16931UblXmlBuilder(flags.Object),
            options,
            new MockPeppolAccessPointClient(),
            new HostedPeppolAccessPointClient(options, new HttpClient(handler)),
            new InMemoryPeppolSubmissionStore(),
            featureFlags: flags.Object);

        var row = await service.SubmitAsync(Guid.NewGuid(), ValidDocument());

        Assert.Equal(0, handler.Calls);
        Assert.NotEqual(PeppolSubmissionStatus.Sent, row.Status);
        Assert.NotEqual(PeppolSubmissionStatus.Ack, row.Status);
    }

    [Fact]
    public void Resolve_Peppol_IsDisabledWithSourceReserved()
    {
        var factory = new Mock<IDbContextFactory<AppDbContext>>(MockBehavior.Strict);
        var service = new FeatureFlagService(
            factory.Object,
            Options.Create(new FeatureFlagsOptions()).ToMonitor(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IAuditLogService>(),
            NullLogger<FeatureFlagService>.Instance,
            new CountryProfileRegistry());

        var resolved = service.Resolve(
            FeatureFlagNames.EInvoicingPeppol,
            Guid.NewGuid(),
            tenantRow: null,
            globalRow: null,
            profile: null,
            loadFromDb: true);

        Assert.False(resolved.Enabled);
        Assert.Equal("reserved", resolved.Source);
        Assert.Equal(FeatureFlagSources.Reserved, resolved.Source);
        factory.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(CountryProfileCodes.Austria)]
    [InlineData(CountryProfileCodes.Germany)]
    [InlineData(CountryProfileCodes.Switzerland)]
    [InlineData(CountryProfileCodes.EuDefault)]
    public void TryGet_Peppol_ReturnsFalse(string countryCode)
    {
        var profile = new CountryProfileRegistry().Get(countryCode);
        var found = CountryFeatureFlagDefaults.TryGet(FeatureFlagNames.EInvoicingPeppol, profile, out var enabled);

        Assert.False(found);
        Assert.False(enabled);
    }

    [Fact]
    public void Source_KeepsPeppolReserved()
    {
        var backendRoot = FindBackendRoot();
        var names = File.ReadAllText(Path.Combine(backendRoot, "Services", "FeatureFlags", "FeatureFlagNames.cs"));
        var all = ArrayBody(names, "public static readonly IReadOnlyList<string> All");
        var reserved = ArrayBody(names, "public static readonly IReadOnlyList<string> Reserved");

        Assert.DoesNotContain("EInvoicingPeppol", all, StringComparison.Ordinal);
        Assert.DoesNotContain("EInvoicing.Peppol", all, StringComparison.Ordinal);
        Assert.Contains("EInvoicingPeppol", reserved, StringComparison.Ordinal);

        var options = File.ReadAllText(Path.Combine(backendRoot, "Configuration", "FeatureFlagsOptions.cs"));
        Assert.DoesNotContain("Peppol", options, StringComparison.OrdinalIgnoreCase);

        var defaults = File.ReadAllText(Path.Combine(backendRoot, "Services", "FeatureFlags", "CountryFeatureFlagDefaults.cs"));
        Assert.DoesNotContain("EInvoicingPeppol", defaults, StringComparison.Ordinal);
        Assert.DoesNotContain("EInvoicing.Peppol", defaults, StringComparison.Ordinal);
    }

    private static string ArrayBody(string source, string declaration)
    {
        var start = source.IndexOf(declaration, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing declaration: {declaration}");
        var open = source.IndexOf('[', start);
        var close = source.IndexOf("];", open, StringComparison.Ordinal);
        Assert.True(open >= 0 && close > open, $"Missing array body for {declaration}");
        return source[open..close];
    }

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KasseAPI_Final.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private static InvoiceDocumentDto ValidDocument() => new()
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

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
