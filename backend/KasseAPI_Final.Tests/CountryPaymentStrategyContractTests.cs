using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Austria;
using KasseAPI_Final.Services.Countries.Strategies.EuDefault;
using KasseAPI_Final.Services.Countries.Strategies.Germany;
using KasseAPI_Final.Services.Countries.Strategies.Switzerland;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CountryPaymentStrategyContractTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();

    private static TaxStrategyResolver Resolver() => new(
    [
        new AustriaTaxStrategy(),
        new GermanyTaxStrategy(),
        new SwitzerlandTaxStrategy(),
        new EuDefaultTaxStrategy(),
    ]);

    [Fact]
    public void DeTenantPayment_ResolvesGermanyTaxStrategy()
    {
        var strategy = Resolver().Resolve(
            Profiles.Get(CountryProfileCodes.Germany),
            VatRegime.DE_USTG_STANDARD);

        Assert.IsType<GermanyTaxStrategy>(strategy);
    }

    [Fact]
    public void AtTenantPayment_ResolvesAustriaTaxStrategy()
    {
        var strategy = Resolver().Resolve(
            Profiles.Get(CountryProfileCodes.Austria),
            VatRegime.AT_RKSV_STANDARD);

        Assert.IsType<AustriaTaxStrategy>(strategy);
    }

    [Fact]
    public void UnknownPair_ThrowsUnknownTaxRegime_AndDoesNotReturnAustria()
    {
        var ex = Assert.Throws<UnknownTaxRegimeException>(() =>
            Resolver().Resolve(
                Profiles.Get(CountryProfileCodes.Germany),
                VatRegime.CH_MWST_STANDARD));

        Assert.Equal(UnknownTaxRegimeException.Code, ex.ErrorCode);
    }

    [Fact]
    public void DeReceiptSequence_UsesItsOwnTable()
    {
        var source = File.ReadAllText(Path.Combine(
            FindBackendRoot(),
            "Services",
            "Countries",
            "Strategies",
            "Germany",
            "DeReceiptSequenceService.cs"));
        var invoice = File.ReadAllText(Path.Combine(
            FindBackendRoot(),
            "Services",
            "Countries",
            "Strategies",
            "Germany",
            "GermanyStrategies.cs"));

        Assert.Contains("de_receipt_sequences", source, StringComparison.Ordinal);
        Assert.DoesNotContain("receipt_sequences", source.Replace("de_receipt_sequences", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("AllocateNextAsync", invoice, StringComparison.Ordinal);
        Assert.Contains("FormatDeBelegNr", invoice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReachablePaymentAndInvoiceMethods_DoNotThrowNotImplemented()
    {
        var profiles = Profiles;
        var cases = new (ITaxStrategy Tax, IInvoiceStrategy Invoice, CountryProfile Profile, VatRegime Regime, TaxLineItemInput Line)[]
        {
            (
                new GermanyTaxStrategy(),
                new GermanyInvoiceStrategy(),
                profiles.Get(CountryProfileCodes.Germany),
                VatRegime.DE_USTG_STANDARD,
                Line(CountryProfileCodes.Germany)),
            (
                new SwitzerlandTaxStrategy(),
                new SwitzerlandInvoiceStrategy(),
                profiles.Get(CountryProfileCodes.Switzerland),
                VatRegime.CH_MWST_STANDARD,
                Line(CountryProfileCodes.Switzerland)),
            (
                new EuDefaultTaxStrategy(),
                new EuDefaultInvoiceStrategy(),
                profiles.Get(CountryProfileCodes.EuDefault),
                VatRegime.NON_EU,
                TaxLineItemInput.FromVatPercent(100m, 1, 0m)),
        };

        foreach (var item in cases)
        {
            var context = new TaxCalculationContext
            {
                CountryProfile = item.Profile,
                VatRegime = item.Regime,
                TaxExempt = false,
            };
            var company = Company(item.Profile.Code, item.Regime);
            var payment = Payment();

            await InvokeAsync(() => Task.FromResult(item.Tax.CalculateTax([item.Line], context)));
            await InvokeAsync(() => Task.FromResult(item.Tax.ValidateVatId(company.VatId, item.Profile)));
            await InvokeAsync(() => Task.FromResult(item.Tax.DetermineInvoiceFields(company, null)));
            await InvokeAsync(() => Task.FromResult(item.Invoice.GetMandatoryDisclosures(company, null)));
            await InvokeAsync(() => item.Invoice.BuildInvoiceDocumentAsync(payment, company, null));
        }
    }

    private static TaxLineItemInput Line(string country)
    {
        var rate = new CountryTaxTypeRegistry()
            .Get(country)
            .Single(type => type.Code == CountryTaxTypeCodes.Standard)
            .Rate;
        return TaxLineItemInput.FromVatPercent(100m + rate, 1, rate);
    }

    private static async Task InvokeAsync(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (NotImplementedException ex)
        {
            Assert.Fail(ex.Message);
        }
    }

    private static CompanySettings Company(string country, VatRegime regime) => new()
    {
        Country = country,
        VatRegime = regime,
        CompanyName = "Mandant",
        CompanyAddress = "Adresse",
        CompanyTaxNumber = "TAX",
        VatId = country == CountryProfileCodes.Switzerland ? "CHE-123.456.789 MWST" : "DE123456789",
        BankAccountNumber = country == CountryProfileCodes.Switzerland ? "CH9300762011623852957" : null,
        Currency = country == CountryProfileCodes.Switzerland ? "CHF" : "EUR",
    };

    private static PaymentDetails Payment() => new()
    {
        ReceiptNumber = "RE-1",
        TotalAmount = 100m,
        TaxAmount = 0m,
        CashierId = "c1",
        PaymentMethodRaw = "0",
        Steuernummer = "DE123456789",
        CashRegisterId = Guid.NewGuid(),
        TaxDetails = JsonDocument.Parse("{}"),
    };

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Services", "Countries", "Strategies", "Germany", "DeReceiptSequenceService.cs");
            if (File.Exists(candidate))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Backend project root was not found from the test output directory.");
    }
}
