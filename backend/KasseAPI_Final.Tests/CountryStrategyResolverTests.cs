using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Austria;
using KasseAPI_Final.Services.Countries.Strategies.EuDefault;
using KasseAPI_Final.Services.Countries.Strategies.Germany;
using KasseAPI_Final.Services.Countries.Strategies.Switzerland;
using KasseAPI_Final.Services.Offline;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Selection contract for the country strategies. The resolvers must fail closed: an unsupported
/// country / regime pair may never fall back to Austria, because that would run RKSV for a non-AT
/// mandant.
/// </summary>
public sealed class CountryStrategyResolverTests
{
    private static readonly ICountryProfileRegistry Registry = new CountryProfileRegistry();

    private static ITaxStrategyResolver TaxResolver() =>
        new TaxStrategyResolver(
        [
            new AustriaTaxStrategy(),
            new GermanyTaxStrategy(),
            new SwitzerlandTaxStrategy(),
            new EuDefaultTaxStrategy(),
        ]);

    private static IInvoiceStrategyResolver InvoiceResolver() =>
        new InvoiceStrategyResolver(
        [
            new AustriaInvoiceStrategy(Mock.Of<ISequenceReservationService>(), Mock.Of<IReceiptService>()),
            new GermanyInvoiceStrategy(),
            new SwitzerlandInvoiceStrategy(),
            new EuDefaultInvoiceStrategy(),
        ]);

    [Fact]
    public void TaxResolver_Austria_WithAustrianRegime_ResolvesAustrianStrategy()
    {
        var strategy = TaxResolver().Resolve(
            Registry.Get(CountryProfileCodes.Austria),
            VatRegime.AT_RKSV_STANDARD);

        Assert.IsType<AustriaTaxStrategy>(strategy);
        Assert.Equal(CountryProfileCodes.Austria, strategy.CountryCode);
    }

    [Fact]
    public void TaxResolver_Germany_WithGermanRegime_ResolvesGermanStrategy()
    {
        var strategy = TaxResolver().Resolve(
            Registry.Get(CountryProfileCodes.Germany),
            VatRegime.DE_USTG_STANDARD);

        Assert.IsType<GermanyTaxStrategy>(strategy);
        Assert.Equal(CountryProfileCodes.Germany, strategy.CountryCode);
    }

    [Fact]
    public void InvoiceResolver_Austria_WithAustrianRegime_ResolvesAustrianStrategy()
    {
        var strategy = InvoiceResolver().Resolve(
            Registry.Get(CountryProfileCodes.Austria),
            VatRegime.AT_RKSV_STANDARD);

        Assert.IsType<AustriaInvoiceStrategy>(strategy);
    }

    [Fact]
    public void InvoiceResolver_Germany_WithGermanRegime_ResolvesGermanStrategy()
    {
        var strategy = InvoiceResolver().Resolve(
            Registry.Get(CountryProfileCodes.Germany),
            VatRegime.DE_USTG_STANDARD);

        Assert.IsType<GermanyInvoiceStrategy>(strategy);
    }

    [Fact]
    public void TaxResolver_EuDefault_WithEuRegime_ResolvesEuStrategy()
    {
        var strategy = TaxResolver().Resolve(
            Registry.Get(CountryProfileCodes.EuDefault),
            VatRegime.EU_REVERSE_CHARGE);

        Assert.IsType<EuDefaultTaxStrategy>(strategy);
        Assert.Equal(CountryProfileCodes.EuDefault, strategy.CountryCode);
    }

    [Fact]
    public void InvoiceResolver_EuDefault_WithEuRegime_ResolvesEuStrategy()
    {
        var strategy = InvoiceResolver().Resolve(
            Registry.Get(CountryProfileCodes.EuDefault),
            VatRegime.EU_OSS);

        Assert.IsType<EuDefaultInvoiceStrategy>(strategy);
    }

    [Theory]
    [InlineData("AT", VatRegime.DE_USTG_STANDARD)]
    [InlineData("AT", VatRegime.CH_MWST_STANDARD)]
    [InlineData("DE", VatRegime.AT_RKSV_STANDARD)]
    [InlineData("CH", VatRegime.EU_OSS)]
    [InlineData("CH", VatRegime.EU_REVERSE_CHARGE)]
    [InlineData("EU_DEFAULT", VatRegime.AT_RKSV_STANDARD)]
    public void Resolvers_Throw_WhenRegimeIsNotAllowedForTheCountry(string countryCode, VatRegime regime)
    {
        var profile = Registry.Get(countryCode);

        var taxError = Assert.Throws<UnknownTaxRegimeException>(() => TaxResolver().Resolve(profile, regime));
        var invoiceError = Assert.Throws<UnknownTaxRegimeException>(() => InvoiceResolver().Resolve(profile, regime));

        Assert.Equal(UnknownTaxRegimeException.Code, taxError.ErrorCode);
        Assert.Equal(UnknownTaxRegimeException.Code, invoiceError.ErrorCode);
        Assert.Equal(countryCode, taxError.CountryCode);
        Assert.Equal(regime, taxError.VatRegime);
    }

    [Fact]
    public void Resolvers_Throw_WhenNoStrategyIsRegisteredForTheCountry()
    {
        var austria = Registry.Get(CountryProfileCodes.Austria);
        var taxResolver = new TaxStrategyResolver([new GermanyTaxStrategy()]);
        var invoiceResolver = new InvoiceStrategyResolver([new GermanyInvoiceStrategy()]);

        Assert.Throws<UnknownTaxRegimeException>(() => taxResolver.Resolve(austria, VatRegime.AT_RKSV_STANDARD));
        Assert.Throws<UnknownTaxRegimeException>(() => invoiceResolver.Resolve(austria, VatRegime.AT_RKSV_STANDARD));
    }

    [Fact]
    public void TaxResolver_IsCaseInsensitiveOnProfileCode()
    {
        // Registry lookups already trim/lower; the resolver must not reintroduce case sensitivity.
        var strategy = TaxResolver().Resolve(Registry.GetOrDefault("at"), VatRegime.AT_RKSV_STANDARD);

        Assert.IsType<AustriaTaxStrategy>(strategy);
    }

    [Fact]
    public void TaxResolver_Switzerland_WithSwissRegime_ResolvesSwissStrategy()
    {
        var strategy = TaxResolver().Resolve(
            Registry.Get(CountryProfileCodes.Switzerland),
            VatRegime.CH_MWST_STANDARD);

        Assert.IsType<SwitzerlandTaxStrategy>(strategy);
        Assert.Equal(CountryProfileCodes.Switzerland, strategy.CountryCode);
    }

    [Fact]
    public void InvoiceResolver_Switzerland_WithSwissRegime_ResolvesSwissStrategy()
    {
        var strategy = InvoiceResolver().Resolve(
            Registry.Get(CountryProfileCodes.Switzerland),
            VatRegime.CH_MWST_STANDARD);

        Assert.IsType<SwitzerlandInvoiceStrategy>(strategy);
    }

    [Fact]
    public void EuDefaultTaxStrategy_ProjectFiscalTaxSets_StillThrows()
    {
        var ex = Assert.Throws<NotImplementedException>(() =>
            new EuDefaultTaxStrategy().ProjectFiscalTaxSets("{}", 0m));

        Assert.Contains(CountryStrategyDocs.EuDefault, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GermanyTaxStrategy_ProjectFiscalTaxSets_StillThrows()
    {
        var ex = Assert.Throws<NotImplementedException>(() =>
            new GermanyTaxStrategy().ProjectFiscalTaxSets("{}", 0m));

        Assert.Contains("docs/FISCAL_GERMANY.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SwitzerlandTaxStrategy_ProjectFiscalTaxSets_StillThrows()
    {
        var ex = Assert.Throws<NotImplementedException>(() =>
            new SwitzerlandTaxStrategy().ProjectFiscalTaxSets("{}", 0m));

        Assert.Contains("docs/FISCAL_SWITZERLAND.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EuDefaultInvoiceStrategy_AllocateReceiptNumber_StillThrows()
    {
        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            new EuDefaultInvoiceStrategy().AllocateReceiptNumberAsync(
                new ReceiptNumberAllocationContext { CashRegisterId = Guid.NewGuid() }));

        Assert.Contains(CountryStrategyDocs.EuDefault, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GermanyInvoiceStrategy_AllocateReceiptNumber_StillThrows()
    {
        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            new GermanyInvoiceStrategy().AllocateReceiptNumberAsync(
                new ReceiptNumberAllocationContext { CashRegisterId = Guid.NewGuid() }));

        Assert.Contains("docs/FISCAL_GERMANY.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SwitzerlandInvoiceStrategy_AllocateReceiptNumber_StillThrows()
    {
        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            new SwitzerlandInvoiceStrategy().AllocateReceiptNumberAsync(
                new ReceiptNumberAllocationContext { CashRegisterId = Guid.NewGuid() }));

        Assert.Contains("docs/FISCAL_SWITZERLAND.md", ex.Message, StringComparison.Ordinal);
    }
}
