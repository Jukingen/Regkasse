using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiscalSignatureRouterChTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();

    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalMwstCh, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static FiscalSignatureContext Context() => new(
        new CountryStrategyBinding(
            new CompanySettings
            {
                TenantId = Guid.NewGuid(),
                Country = CountryProfileCodes.Switzerland,
                VatRegime = VatRegime.CH_MWST_STANDARD,
                CompanyName = "CH GmbH",
                CompanyAddress = "Bahnhofstrasse 1",
                CompanyTaxNumber = "CHE-123.456.789 MWST",
                BankAccountNumber = "CH9300762011623852957",
                Currency = "CHF",
            },
            Profiles.Get(CountryProfileCodes.Switzerland),
            VatRegime.CH_MWST_STANDARD,
            UsedLegacyFallback: false),
        [TaxLineItemInput.FromVatPercent(108.1m, 1, 8.1m)],
        108.1m,
        "RE-CH-1");

    [Fact]
    public async Task ChTenant_FlagOn_ReturnsMwstAndQr_WithoutSignature()
    {
        var router = new FiscalSignatureRouter(Flags(true));
        var result = await router.SignAsync(Context());

        Assert.Equal(FiscalSignatureRouter.ChProvider, result.Provider);
        Assert.Null(result.Signature);
        Assert.True(result.TotalVat > 0m);
        Assert.StartsWith("SPC\n", result.SwissQrText, StringComparison.Ordinal);
        Assert.Contains("\nNON\n", result.SwissQrText, StringComparison.Ordinal);
        Assert.Contains("RE-CH-1", result.SwissQrText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChTenant_FlagOff_ThrowsFeatureDisabled()
    {
        var router = new FiscalSignatureRouter(Flags(false));
        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() => router.SignAsync(Context()));

        Assert.Equal(FeatureFlagNames.FiscalMwstCh, ex.FeatureName);
    }
}
