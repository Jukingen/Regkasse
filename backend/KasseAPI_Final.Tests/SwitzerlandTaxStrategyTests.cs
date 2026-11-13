using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Switzerland;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SwitzerlandTaxStrategyTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();
    private static readonly ICountryTaxTypeRegistry RateCatalog = new CountryTaxTypeRegistry();

    private static TaxCalculationContext ChContext() => new()
    {
        CountryProfile = Profiles.Get(CountryProfileCodes.Switzerland),
        VatRegime = VatRegime.CH_MWST_STANDARD,
        TaxExempt = false,
    };

    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalMwstCh, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static SwitzerlandTaxStrategy Strategy(
        bool enabled = true,
        IVatIdValidator? vatIdValidator = null) =>
        new(
            RateCatalog,
            vatIdValidator ?? new VatIdValidator(new DisabledViesClient()),
            Flags(enabled));

    [Fact]
    public void CalculateTax_UsesRegistryMwstRates_WithoutAustrianBuckets()
    {
        var catalog = new CountryTaxTypeRegistry().Get(CountryProfileCodes.Switzerland);
        var standard = catalog.Single(t => t.Code == CountryTaxTypeCodes.Standard).Rate;
        var reduced = catalog.Single(t => t.Code == CountryTaxTypeCodes.Reduced1).Rate;
        var lodging = catalog.Single(t => t.Code == CountryTaxTypeCodes.Lodging).Rate;
        var expectedStandard = CartMoneyHelper.ComputeLine(100m + standard, 1, standard);
        var expectedReduced = CartMoneyHelper.ComputeLine(100m + reduced, 1, reduced);
        var expectedLodging = CartMoneyHelper.ComputeLine(100m + lodging, 1, lodging);

        var result = Strategy().CalculateTax(
            [
                TaxLineItemInput.FromVatPercent(100m + standard, 1, standard),
                TaxLineItemInput.FromVatPercent(100m + reduced, 1, reduced),
                TaxLineItemInput.FromVatPercent(100m + lodging, 1, lodging),
            ],
            ChContext());

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(expectedStandard, result.Lines[0]);
        Assert.Equal(expectedReduced, result.Lines[1]);
        Assert.Equal(expectedLodging, result.Lines[2]);
        Assert.Equal(expectedStandard.LineTax, result.TaxDetails[CountryTaxTypeCodes.Standard]);
        Assert.Equal(expectedReduced.LineTax, result.TaxDetails[CountryTaxTypeCodes.Reduced1]);
        Assert.Equal(expectedLodging.LineTax, result.TaxDetails[CountryTaxTypeCodes.Lodging]);
        Assert.Equal(standard, result.TaxSummary.Single(s => s.TaxRatePct == standard).TaxRatePct);
        Assert.DoesNotContain(TaxTypes.Standard.ToString(), result.TaxDetails.Keys);
    }

    [Fact]
    public void CalculateTax_DoesNotHardcodeSeededPercents()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? found = null;
        while (dir is not null && found is null)
        {
            var candidate = Path.Combine(dir.FullName, "Services", "Countries", "Strategies", "Switzerland", "SwitzerlandStrategies.cs");
            if (File.Exists(candidate))
                found = File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        Assert.NotNull(found);
        Assert.DoesNotContain("8.1", found, StringComparison.Ordinal);
        Assert.DoesNotContain("2.6", found, StringComparison.Ordinal);
        Assert.DoesNotContain("3.8", found, StringComparison.Ordinal);
    }

    [Fact]
    public void CalculateTax_Kleinunternehmer_ChargesZero_AndIgnoresBuyerVatId()
    {
        var gross = 108.1m;
        var expected = CartMoneyHelper.ComputeLine(gross, 1, 0m);
        var context = new TaxCalculationContext
        {
            CountryProfile = Profiles.Get(CountryProfileCodes.Switzerland),
            VatRegime = VatRegime.CH_KLEINUNTERNEHMER,
            TaxExempt = true,
            BuyerVatId = "CHE-123.456.789 MWST",
        };

        var result = Strategy().CalculateTax(
            [TaxLineItemInput.FromVatPercent(gross, 1, 8.1m)],
            context);

        Assert.Equal(expected, result.Lines[0]);
        Assert.Equal(0m, result.Totals.TotalVat);
        Assert.Equal(gross, result.Totals.TotalGross);
        Assert.Equal(0m, result.TaxDetails[CountryTaxTypeCodes.Zero]);
    }

    [Fact]
    public void CalculateTax_TaxExempt_ChargesZero()
    {
        var context = new TaxCalculationContext
        {
            CountryProfile = Profiles.Get(CountryProfileCodes.Switzerland),
            VatRegime = VatRegime.CH_MWST_STANDARD,
            TaxExempt = true,
        };

        var result = Strategy().CalculateTax(
            [TaxLineItemInput.FromVatPercent(102.6m, 1, 2.6m)],
            context);

        Assert.Equal(0m, result.Totals.TotalVat);
        Assert.Equal(102.6m, result.Totals.TotalNet);
    }

    [Fact]
    public void CalculateTax_ReverseCharge_Throws()
    {
        var context = new TaxCalculationContext
        {
            CountryProfile = Profiles.Get(CountryProfileCodes.Switzerland),
            VatRegime = VatRegime.EU_REVERSE_CHARGE,
            TaxExempt = false,
            BuyerVatId = "DE123456789",
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            Strategy().CalculateTax(
                [TaxLineItemInput.FromVatPercent(108.1m, 1, 8.1m)],
                context));

        Assert.Contains("reverse charge", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CalculateTax_BuyerVatId_DoesNotZeroStandardMwst()
    {
        var expected = CartMoneyHelper.ComputeLine(108.1m, 1, 8.1m);
        var context = new TaxCalculationContext
        {
            CountryProfile = Profiles.Get(CountryProfileCodes.Switzerland),
            VatRegime = VatRegime.CH_MWST_STANDARD,
            TaxExempt = false,
            BuyerVatId = "CHE-123.456.789 MWST",
        };

        var result = Strategy().CalculateTax(
            [TaxLineItemInput.FromVatPercent(108.1m, 1, 8.1m)],
            context);

        Assert.Equal(expected.LineTax, result.Totals.TotalVat);
        Assert.Equal(expected.LineTax, result.TaxDetails[CountryTaxTypeCodes.Standard]);
    }

    [Fact]
    public void CalculateTax_RejectsRksvTaxTypeLines()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Strategy().CalculateTax(
                [TaxLineItemInput.FromTaxType(10m, 1, TaxTypes.Standard)],
                ChContext()));

        Assert.Contains("CountryTaxType", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CalculateTax_FlagOff_ThrowsFeatureDisabled()
    {
        var ex = Assert.Throws<FeatureDisabledException>(() =>
            Strategy(enabled: false).CalculateTax(
                [TaxLineItemInput.FromVatPercent(108.1m, 1, 8.1m)],
                ChContext()));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.FiscalMwstCh, ex.FeatureName);
    }

    [Fact]
    public void ValidateVatId_DelegatesToIVatIdValidator_Once()
    {
        var profile = Profiles.Get(CountryProfileCodes.Switzerland);
        var validator = new Mock<IVatIdValidator>(MockBehavior.Strict);
        validator
            .Setup(v => v.Validate("CHE-123.456.789 MWST", profile))
            .Returns(VatIdValidationResult.Valid("CHE-123.456.789 MWST"));

        var result = Strategy(vatIdValidator: validator.Object)
            .ValidateVatId("CHE-123.456.789 MWST", profile);

        Assert.True(result.IsValid);
        Assert.Equal("CHE-123.456.789 MWST", result.VatId);
        validator.Verify(v => v.Validate("CHE-123.456.789 MWST", profile), Times.Once);
        validator.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProjectFiscalTaxSets_ReadsTheChCatalogRate()
    {
        var standard = RateCatalog.Get(CountryProfileCodes.Switzerland)
            .Single(type => type.Code == CountryTaxTypeCodes.Standard);
        var gross = 100m + standard.Rate;
        var tax = CartMoneyHelper.ComputeLine(gross, 1, standard.Rate).LineTax;
        var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, decimal>
        {
            [CountryTaxTypeCodes.Standard] = tax,
        });

        var sets = Strategy().ProjectFiscalTaxSets(json, gross);

        Assert.NotNull(sets);
        Assert.Equal(gross, sets!.Normal);
        Assert.Equal(0m, sets.Ermaessigt1);
        Assert.Equal(0m, sets.Besonders);
    }

    [Fact]
    public void ProjectFiscalTaxSets_EmptyCatalog_DoesNotUseAustrianRates()
    {
        var empty = new Mock<ICountryTaxTypeRegistry>();
        empty.Setup(r => r.Get(CountryProfileCodes.Switzerland)).Returns(Array.Empty<CountryTaxType>());
        var strategy = new SwitzerlandTaxStrategy(
            empty.Object,
            new VatIdValidator(new DisabledViesClient()),
            Flags(true));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            strategy.ProjectFiscalTaxSets("{\"STANDARD\":8.1}", 108.1m));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        empty.Verify(r => r.Get(CountryProfileCodes.Switzerland), Times.Once);
        empty.Verify(r => r.Get(CountryProfileCodes.Austria), Times.Never);
    }
}
