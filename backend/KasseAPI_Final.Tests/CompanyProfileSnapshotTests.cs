using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public class CompanyProfileSnapshotTests
{
    private static CompanyProfileOptions LiveProfile() => new()
    {
        CompanyName = "Neue GmbH",
        TaxNumber = "ATU99999999",
        Street = "Neue Straße 1",
        ZipCode = "1020",
        City = "Wien",
        FooterText = "",
    };

    [Fact]
    public void ApplySnapshot_FreezesCompanyNameAndAddressOnPayment()
    {
        var profile = LiveProfile();
        var payment = new PaymentDetails { Steuernummer = profile.TaxNumber };

        CompanyProfileMapper.ApplySnapshot(payment, profile);

        Assert.Equal("Neue GmbH", payment.CompanyName);
        Assert.Equal("Neue Straße 1, 1020 Wien", payment.CompanyAddress);
    }

    [Fact]
    public void CopySnapshotFromOriginal_PreservesHistoricalCompanyOnStorno()
    {
        var live = LiveProfile();
        var original = new PaymentDetails
        {
            CompanyName = "Alte GmbH",
            CompanyAddress = "Alte Gasse 5, 1010 Wien",
            Steuernummer = "ATU12345678",
        };
        var storno = new PaymentDetails();

        CompanyProfileMapper.CopySnapshotFromOriginal(storno, original, live);

        Assert.Equal("Alte GmbH", storno.CompanyName);
        Assert.Equal("Alte Gasse 5, 1010 Wien", storno.CompanyAddress);
    }

    [Fact]
    public void ResolveForDisplay_PrefersPaymentSnapshotOverLiveSettings()
    {
        var live = LiveProfile();
        var payment = new PaymentDetails
        {
            CompanyName = "Alte GmbH",
            CompanyAddress = "Alte Gasse 5, 1010 Wien",
            Steuernummer = "ATU12345678",
        };

        var (name, address, tax) = CompanyProfileMapper.ResolveForDisplay(payment, live);

        Assert.Equal("Alte GmbH", name);
        Assert.Equal("Alte Gasse 5, 1010 Wien", address);
        Assert.Equal("ATU12345678", tax);
    }

    [Fact]
    public void ResolveForDisplay_FallsBackToLiveProfileForLegacyPayments()
    {
        var live = LiveProfile();
        var payment = new PaymentDetails { Steuernummer = "ATU12345678" };

        var (name, address, tax) = CompanyProfileMapper.ResolveForDisplay(payment, live);

        Assert.Equal("Neue GmbH", name);
        Assert.Equal("Neue Straße 1, 1020 Wien", address);
        Assert.Equal("ATU12345678", tax);
    }
}
