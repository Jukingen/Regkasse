using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public class ProductLocalizationTests
{
    [Fact]
    public void ResolveName_UsesEnglishWhenRequested()
    {
        var product = new Product
        {
            Name = "Pizza Margherita",
            NameDe = "Pizza Margherita",
            NameEn = "Margherita Pizza",
            NameTr = "Margherita Pizza",
        };

        Assert.Equal("Margherita Pizza", ProductLocalization.ResolveName(product, "en"));
        Assert.Equal("Pizza Margherita", ProductLocalization.ResolveName(product, "de"));
    }

    [Fact]
    public void SyncCanonicalFields_SetsLegacyNameFromGerman()
    {
        var product = new Product
        {
            Name = "old",
            NameDe = "Pizza Salami",
            DescriptionDe = "mit Salami",
        };

        ProductLocalization.SyncCanonicalFields(product);

        Assert.Equal("Pizza Salami", product.Name);
        Assert.Equal("mit Salami", product.Description);
    }
}
