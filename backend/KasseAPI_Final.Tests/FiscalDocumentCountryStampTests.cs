using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiscalDocumentCountryStampTests
{
    [Fact]
    public void Apply_StampsNormalizedCountryAndRegime()
    {
        var payment = new PaymentDetails();
        FiscalDocumentCountryStamp.Apply(payment, " at ", VatRegime.AT_RKSV_STANDARD);

        Assert.Equal("AT", payment.CountryCodeAtIssue);
        Assert.Equal(VatRegime.AT_RKSV_STANDARD, payment.VatRegimeAtIssue);
    }

    [Fact]
    public void CopyFromPayment_DoesNotRewriteWhenSourceAlreadyStamped()
    {
        var original = new PaymentDetails
        {
            CountryCodeAtIssue = "AT",
            VatRegimeAtIssue = VatRegime.AT_RKSV_STANDARD,
        };
        var invoice = new Invoice();
        var deProfile = new CountryProfileRegistry().Get("DE");
        var fallback = new CountryStrategyBinding(
            new CompanySettings { Country = "DE", VatRegime = VatRegime.DE_USTG_STANDARD },
            deProfile,
            VatRegime.DE_USTG_STANDARD,
            UsedLegacyFallback: false);

        FiscalDocumentCountryStamp.CopyFromPayment(invoice, original, fallback);

        Assert.Equal("AT", invoice.CountryCodeAtIssue);
        Assert.Equal(VatRegime.AT_RKSV_STANDARD, invoice.VatRegimeAtIssue);
    }

    [Fact]
    public void CopyFromPayment_FallsBackToLiveBindingWhenLegacyNull()
    {
        var original = new PaymentDetails();
        var invoice = new Invoice();
        var deProfile = new CountryProfileRegistry().Get("DE");
        var fallback = new CountryStrategyBinding(
            new CompanySettings { Country = "DE", VatRegime = VatRegime.DE_USTG_STANDARD },
            deProfile,
            VatRegime.DE_USTG_STANDARD,
            UsedLegacyFallback: false);

        FiscalDocumentCountryStamp.CopyFromPayment(invoice, original, fallback);

        Assert.Equal("DE", invoice.CountryCodeAtIssue);
        Assert.Equal(VatRegime.DE_USTG_STANDARD, invoice.VatRegimeAtIssue);
    }
}
