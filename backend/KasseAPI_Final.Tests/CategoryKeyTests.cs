using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CategoryKeyTests
{
    [Theory]
    [InlineData("Pizza-mittel", "pizza-mittel")]
    [InlineData("Alkoholfreie-Getrnke", "alkoholfreie-getrnke")]
    [InlineData("Salate", "salate")]
    public void FromDisplayName_ProducesStableSlug(string displayName, string expectedKey)
    {
        Assert.Equal(expectedKey, CategoryKey.FromDisplayName(displayName));
    }

    [Theory]
    [InlineData("Salate", RksvProductCategory.Food)]
    [InlineData("Pizza-mittel", RksvProductCategory.Food)]
    [InlineData("Alkoholfreie-Getrnke", RksvProductCategory.Beverage)]
    [InlineData("Wein", RksvProductCategory.AlcoholicBeverage)]
    [InlineData("Tabakwaren", RksvProductCategory.Tobacco)]
    public void InferFiscalCategory_MapsKnownCatalogNames(string name, RksvProductCategory expected)
    {
        Assert.Equal(expected, CategoryKey.InferFiscalCategory(name));
    }

    [Theory]
    [InlineData("pizza-mittel", true)]
    [InlineData("salate", true)]
    [InlineData("Pizza Mittel", false)]
    [InlineData("", false)]
    public void IsValid_AcceptsLowercaseSlugKeys(string key, bool expected)
    {
        Assert.Equal(expected, CategoryKey.IsValid(key));
    }
}
