using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiscalSignatureRouterEuTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();

    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.EInvoicingEn16931, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static FiscalSignatureContext Context() => new(
        new CountryStrategyBinding(
            new CompanySettings
            {
                TenantId = Guid.NewGuid(),
                Country = CountryProfileCodes.EuDefault,
                VatRegime = VatRegime.EU_REVERSE_CHARGE,
                CompanyName = "EU Seller GmbH",
                VatId = "ATU12345678",
                Currency = "EUR",
            },
            Profiles.Get(CountryProfileCodes.EuDefault),
            VatRegime.EU_REVERSE_CHARGE,
            UsedLegacyFallback: false),
        [TaxLineItemInput.FromVatPercent(121m, 1, 21m)],
        121m,
        "EU-2026-1",
        BuyerName: "Buyer BV",
        BuyerVatId: "DE123456789",
        BuyerCountry: "DE");

    [Fact]
    public async Task EuTenant_FlagOn_ReturnsEn16931AndPeppolStatus()
    {
        var router = new FiscalSignatureRouter(Flags(true));
        var result = await router.SignAsync(Context());

        Assert.Equal(FiscalSignatureRouter.EuProvider, result.Provider);
        Assert.Null(result.Signature);
        Assert.Equal(0m, result.TotalVat);
        Assert.Equal("Validated", result.PeppolStatus);
    }

    [Fact]
    public async Task EuTenant_FlagOff_ThrowsFeatureDisabled()
    {
        var router = new FiscalSignatureRouter(Flags(false));
        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() => router.SignAsync(Context()));

        Assert.Equal(FeatureFlagNames.EInvoicingEn16931, ex.FeatureName);
    }
}
