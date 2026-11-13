using KasseAPI_Final.Models;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ReceiptThankYouMessageTests
{
    [Fact]
    public void Resolve_ReturnsDefault_WhenSettingsNull()
    {
        Assert.Equal(ReceiptThankYouMessage.Default, ReceiptThankYouMessage.Resolve(null));
    }

    [Fact]
    public void Resolve_PrefersThankYouMessage()
    {
        var settings = new CompanySettings
        {
            ThankYouMessage = " Danke! ",
            CompanyDescription = "Legacy",
        };
        Assert.Equal("Danke!", ReceiptThankYouMessage.Resolve(settings));
    }

    [Fact]
    public void Resolve_UsesDefault_WhenThankYouUnset_EvenIfCompanyDescriptionSet()
    {
        var settings = new CompanySettings { CompanyDescription = "Dev company description" };
        Assert.Equal(ReceiptThankYouMessage.Default, ReceiptThankYouMessage.Resolve(settings));
    }

    [Fact]
    public void NormalizeStored_TreatsWhitespaceAsUnset()
    {
        Assert.Null(ReceiptThankYouMessage.NormalizeStored("  "));
        Assert.Null(ReceiptThankYouMessage.NormalizeStored(null));
        Assert.Equal("Hi", ReceiptThankYouMessage.NormalizeStored(" Hi "));
    }
}
