using System.Net;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
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

public sealed class PeppolReservedExitTests
{
    [Fact]
    public void ExitDisabled_ResolvesReservedOff_ForEveryTenant()
    {
        var tenant = Guid.NewGuid();
        var resolved = Resolve(new PeppolReservedExitOptions { Enabled = false, CanaryTenantId = tenant.ToString("D") }, tenant);

        Assert.False(resolved.Enabled);
        Assert.Equal(FeatureFlagSources.Reserved, resolved.Source);
        Assert.Equal("reserved", resolved.Source);
    }

    [Fact]
    public void ExitEnabled_EmptyCanary_ResolvesReservedExitOff()
    {
        var resolved = Resolve(new PeppolReservedExitOptions { Enabled = true, CanaryTenantId = "" }, Guid.NewGuid());

        Assert.False(resolved.Enabled);
        Assert.Equal("reserved_exit", resolved.Source);
    }

    [Fact]
    public void ExitEnabled_NonCanaryTenant_StaysOff()
    {
        var resolved = Resolve(
            new PeppolReservedExitOptions { Enabled = true, CanaryTenantId = Guid.NewGuid().ToString("D") },
            Guid.NewGuid());

        Assert.False(resolved.Enabled);
        Assert.Equal(FeatureFlagSources.ReservedExit, resolved.Source);
    }

    [Fact]
    public void ExitEnabled_CanaryTenant_ResolvesOn()
    {
        var canary = Guid.NewGuid();
        var resolved = Resolve(
            new PeppolReservedExitOptions
            {
                Enabled = true,
                CanaryTenantId = canary.ToString("D"),
                ApprovedBy = "ops",
            },
            canary);

        Assert.True(resolved.Enabled);
        Assert.Equal("reserved_exit_canary", resolved.Source);
    }

    [Fact]
    public void Peppol_StaysReserved_AndOutOfAll()
    {
        Assert.Contains(FeatureFlagNames.EInvoicingPeppol, FeatureFlagNames.Reserved);
        Assert.DoesNotContain(FeatureFlagNames.EInvoicingPeppol, FeatureFlagNames.All);
    }

    [Fact]
    public async Task SubmitAsync_StillMakesZeroHttpCalls()
    {
        var handler = new CountingHandler();
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(flag => flag.IsEnabled(FeatureFlagNames.EInvoicingEn16931, It.IsAny<string?>())).Returns(true);
        var options = Options.Create(new PeppolOptions
        {
            AccessPointMode = "hosted",
            Provider = "hosted",
            BaseUrl = "https://ap.example/v1",
            ReservedExit = new PeppolReservedExitOptions
            {
                Enabled = true,
                CanaryTenantId = Guid.NewGuid().ToString("D"),
            },
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
        Assert.Equal(PeppolSubmissionStatus.Queued, row.Status);
        Assert.Equal(PeppolSubmissionService.ReservedFailureReason, row.Detail);
    }

    private static FeatureFlagService.FlagResolution Resolve(PeppolReservedExitOptions exit, Guid tenantId)
    {
        var factory = new Mock<IDbContextFactory<AppDbContext>>(MockBehavior.Strict);
        var service = new FeatureFlagService(
            factory.Object,
            Options.Create(new FeatureFlagsOptions()).ToMonitor(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IAuditLogService>(),
            NullLogger<FeatureFlagService>.Instance,
            new CountryProfileRegistry(),
            Options.Create(new PeppolOptions { ReservedExit = exit }));

        var resolved = service.Resolve(
            FeatureFlagNames.EInvoicingPeppol,
            tenantId,
            tenantRow: null,
            globalRow: null,
            profile: null,
            loadFromDb: true);
        factory.VerifyNoOtherCalls();
        return resolved;
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
