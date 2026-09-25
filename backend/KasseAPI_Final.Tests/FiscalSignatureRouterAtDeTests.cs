using KasseAPI_Final.Fiscal;
using KasseAPI_Final.Services.Countries.KassenSicherheit;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiscalSignatureRouterAtDeTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();

    private static IFeatureFlagService Flags(bool deEnabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, It.IsAny<string?>()))
            .Returns(deEnabled);
        return mock.Object;
    }

    private static FiscalSignatureContext Context(
        string country,
        VatRegime regime,
        Guid? cashRegisterId = null,
        string? registerNumber = null,
        string? deTssId = null,
        string? deClientId = null) => new(
        new CountryStrategyBinding(
            new CompanySettings
            {
                TenantId = Guid.NewGuid(),
                Country = country,
                VatRegime = regime,
                CompanyName = "Mandant",
                Currency = "EUR",
                DeTssId = deTssId,
                DeClientId = deClientId,
            },
            Profiles.Get(country),
            regime,
            UsedLegacyFallback: false),
        [TaxLineItemInput.FromVatPercent(120m, 1, 20m)],
        120m,
        "AT-1-20260925-1",
        CashRegisterId: cashRegisterId,
        RegisterNumber: registerNumber,
        PrevSignatureValue: "prev",
        TaxDetailsJson: "{}");

    [Fact]
    public async Task AtTenant_CallsTseOnce_AndReturnsFiskalyFields()
    {
        var registerId = Guid.NewGuid();
        var tse = new Mock<ITseService>();
        tse.Setup(x => x.CreateInvoiceSignatureAsync(
                registerId,
                "AT-1-20260925-1",
                120m,
                "1",
                "prev",
                null,
                "{}",
                null))
            .ReturnsAsync(new TseSignatureResult("header.payload.sig", "prev-used", "thumb"));

        var router = new FiscalSignatureRouter(Flags(false), tse: tse.Object);
        var result = await router.SignAsync(Context(
            CountryProfileCodes.Austria,
            VatRegime.AT_RKSV_STANDARD,
            registerId,
            "1"));

        Assert.Equal(FiscalSignatureRouter.AtProvider, result.Provider);
        Assert.Equal("header.payload.sig", result.Signature);
        Assert.Equal("prev-used", result.PrevSignatureValue);
        Assert.Equal("thumb", result.CertificateThumbprint);
        tse.Verify(x => x.CreateInvoiceSignatureAsync(
            registerId,
            "AT-1-20260925-1",
            120m,
            "1",
            "prev",
            null,
            "{}",
            It.IsAny<IDbContextTransaction?>()), Times.Once);
    }

    [Fact]
    public async Task AtTenant_MissingCashRegisterId_Throws()
    {
        var router = new FiscalSignatureRouter(Flags(false), tse: new Mock<ITseService>().Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            router.SignAsync(Context(CountryProfileCodes.Austria, VatRegime.AT_RKSV_STANDARD, registerNumber: "1")));

        Assert.Equal("AT branch requires cashRegisterId and registerNumber", ex.Message);
    }

    [Fact]
    public async Task AtTenant_MissingRegisterNumber_Throws()
    {
        var router = new FiscalSignatureRouter(Flags(false), tse: new Mock<ITseService>().Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            router.SignAsync(Context(
                CountryProfileCodes.Austria,
                VatRegime.AT_RKSV_STANDARD,
                cashRegisterId: Guid.NewGuid())));

        Assert.Equal("AT branch requires cashRegisterId and registerNumber", ex.Message);
    }

    [Fact]
    public async Task DeTenant_FlagOff_ThrowsDeFlagOff()
    {
        var router = new FiscalSignatureRouter(Flags(false));
        var ex = await Assert.ThrowsAsync<FiscalSigningNotAvailableException>(() =>
            router.SignAsync(Context(CountryProfileCodes.Germany, VatRegime.DE_USTG_STANDARD)));

        Assert.Equal(FiscalSigningNotAvailableException.DeFlagOff, ex.Code);
    }

    [Fact]
    public async Task DeTenant_FlagOn_MissingIds_ThrowsDeNotConfigured()
    {
        var previousTss = Environment.GetEnvironmentVariable("KassenSicherheit__TssId");
        var previousClient = Environment.GetEnvironmentVariable("KassenSicherheit__ClientId");
        Environment.SetEnvironmentVariable("KassenSicherheit__TssId", null);
        Environment.SetEnvironmentVariable("KassenSicherheit__ClientId", null);
        try
        {
            var router = new FiscalSignatureRouter(Flags(true));
            var ex = await Assert.ThrowsAsync<FiscalSigningNotAvailableException>(() =>
                router.SignAsync(Context(CountryProfileCodes.Germany, VatRegime.DE_USTG_STANDARD)));

            Assert.Equal(FiscalSigningNotAvailableException.DeNotConfigured, ex.Code);
        }
        finally
        {
            Environment.SetEnvironmentVariable("KassenSicherheit__TssId", previousTss);
            Environment.SetEnvironmentVariable("KassenSicherheit__ClientId", previousClient);
        }
    }

    [Fact]
    public async Task DeTenant_FlagOn_WithIds_StartsAndFinishes()
    {
        var kassen = new Mock<IKassenSicherheitService>();
        kassen.Setup(x => x.StartTransactionAsync(
                It.IsAny<KassenSicherheitStartTransactionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KassenSicherheitTransactionResult(true, "tx-1", "ACTIVE", 1, null, "fiskaly"));
        kassen.Setup(x => x.FinishTransactionAsync(
                It.IsAny<KassenSicherheitFinishTransactionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KassenSicherheitTransactionResult(true, "tx-1", "FINISHED", 2, "sig-de", "fiskaly"));

        var router = new FiscalSignatureRouter(Flags(true), kassen: kassen.Object);
        var context = Context(
            CountryProfileCodes.Germany,
            VatRegime.DE_USTG_STANDARD,
            deTssId: "tss-1",
            deClientId: "client-1") with
        {
            TotalGross = 0m,
            TaxDetailsJson = "{}",
            ReceiptNumber = "DE-dev-1-1",
        };

        var result = await router.SignAsync(context);

        Assert.Equal(FiscalSignatureRouter.DeProvider, result.Provider);
        Assert.Equal("sig-de", result.Signature);
        kassen.Verify(x => x.StartTransactionAsync(
            It.Is<KassenSicherheitStartTransactionRequest>(r => r.TssId == "tss-1" && r.ClientId == "client-1"),
            It.IsAny<CancellationToken>()), Times.Once);
        kassen.Verify(x => x.FinishTransactionAsync(
            It.Is<KassenSicherheitFinishTransactionRequest>(r =>
                r.Receipt != null
                && r.Belegnummer == "DE-dev-1-1"
                && r.Receipt.StandardV1.Receipt.ReceiptType == "RECEIPT"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
