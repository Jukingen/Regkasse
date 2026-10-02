using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Austria;
using KasseAPI_Final.Services.Countries.Strategies.EuDefault;
using KasseAPI_Final.Services.Countries.Vat;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OssVatRateIsolationTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();

    [Fact]
    public void AtDomesticSale_NeverConsultsOssRegistry_EuOssSaleDoes()
    {
        var oss = new Mock<IOssVatRateRegistry>(MockBehavior.Strict);
        oss.Setup(r => r.GetStandardRate("FR")).Returns(20m);

        var resolver = new TaxStrategyResolver(
        [
            new AustriaTaxStrategy(),
            new EuDefaultTaxStrategy(
                Profiles,
                new VatIdValidator(new DisabledViesClient()),
                featureFlags: null,
                oss.Object),
        ]);

        var at = resolver.Resolve(
            Profiles.Get(CountryProfileCodes.Austria),
            VatRegime.AT_RKSV_STANDARD);
        Assert.IsType<AustriaTaxStrategy>(at);

        var domestic = at.CalculateTax(
            [TaxLineItemInput.FromTaxType(12m, 1, TaxTypes.Standard)],
            new TaxCalculationContext
            {
                CountryProfile = Profiles.Get(CountryProfileCodes.Austria),
                VatRegime = VatRegime.AT_RKSV_STANDARD,
            });

        Assert.Equal(TaxTypes.Standard, domestic.Lines[0].TaxType);
        oss.Verify(r => r.GetStandardRate(It.IsAny<string>()), Times.Never);
        oss.Verify(r => r.GetAll(), Times.Never);

        var eu = resolver.Resolve(
            Profiles.Get(CountryProfileCodes.EuDefault),
            VatRegime.EU_OSS);
        Assert.IsType<EuDefaultTaxStrategy>(eu);

        var crossBorder = eu.CalculateTax(
            [TaxLineItemInput.FromVatPercent(120m, 1, 0m)],
            new TaxCalculationContext
            {
                CountryProfile = Profiles.Get(CountryProfileCodes.EuDefault),
                VatRegime = VatRegime.EU_OSS,
                DestinationCountry = "FR",
            });

        Assert.Equal(20m, crossBorder.TaxSummary.Single().TaxRatePct);
        oss.Verify(r => r.GetStandardRate("FR"), Times.Once);
    }
}
